using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Shelf.Core.Search;

/// <summary>A search with a name (F-SRC-8). The query is written in the query language of <see cref="QueryParser"/>.</summary>
public sealed record SavedSearch(string Name, string Query);

/// <summary><c>.shelf/searches.json</c> is damaged, newer than this version, or cannot be written; or a name or a query cannot be kept.</summary>
public sealed class SavedSearchException(string message, bool notFound = false, Exception? innerException = null)
    : Exception(message, innerException)
{
    public bool NotFound { get; } = notFound;
}

/// <summary>
/// The saved searches of a library: <c>.shelf/searches.json</c> (schema 1), the source of truth (F-SRC-8). Names are letters, digits,
/// <c>_</c>, <c>.</c> and <c>-</c>, at most 40, and not case sensitive. A name written as <c>@name</c> in a query stands for the saved
/// query (F-SRC-9, <see cref="Expand"/>). Changes replace the file atomically.
/// </summary>
public sealed partial class SavedSearches
{
    public const string FileName = "searches.json";
    public const int CurrentSchema = 1;
    public const int MaxNameLength = 40;

    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly List<SavedSearch> _items;

    public SavedSearches(IEnumerable<SavedSearch>? items = null) => _items = [.. items ?? []];

    public IReadOnlyList<SavedSearch> Items => _items;

    public static string PathFor(string libraryRoot) => Path.Combine(libraryRoot, ".shelf", FileName);

    /// <summary>Reads the saved searches of a library. A missing file is an empty set.</summary>
    /// <exception cref="SavedSearchException"/>
    public static SavedSearches Load(string libraryRoot) => LoadFile(PathFor(libraryRoot));

    /// <exception cref="SavedSearchException"/>
    public static SavedSearches LoadFile(string path)
    {
        string text;
        try
        {
            text = File.ReadAllText(path, new UTF8Encoding(false, throwOnInvalidBytes: true));
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            return new SavedSearches();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or DecoderFallbackException)
        {
            throw new SavedSearchException($"{path}: cannot read file: {e.Message}", innerException: e);
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("schema", out var schema)
                || schema.ValueKind != JsonValueKind.Number
                || !schema.TryGetInt32(out var version))
            {
                throw new SavedSearchException($"{path}: needs a number 'schema'");
            }

            if (version > CurrentSchema)
            {
                throw new SavedSearchException($"{path}: schema {version} is newer than this version of shelf understands ({CurrentSchema})");
            }

            var items = new List<SavedSearch>();
            if (root.TryGetProperty("searches", out var searches) && searches.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in searches.EnumerateArray())
                {
                    var name = entry.ValueKind == JsonValueKind.Object && entry.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null;
                    var query = entry.ValueKind == JsonValueKind.Object && entry.TryGetProperty("query", out var q) && q.ValueKind == JsonValueKind.String ? q.GetString() : null;
                    if (string.IsNullOrWhiteSpace(name) || query is null)
                    {
                        throw new SavedSearchException($"{path}: every search needs a 'name' and a 'query'");
                    }

                    items.Add(new SavedSearch(name, query));
                }
            }

            return new SavedSearches(items);
        }
        catch (JsonException e)
        {
            throw new SavedSearchException($"{path}: not valid JSON: {e.Message}", innerException: e);
        }
    }

    /// <summary>Writes the file atomically.</summary>
    /// <exception cref="SavedSearchException"/>
    public void Save(string libraryRoot)
    {
        var path = PathFor(libraryRoot);
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                using (var writer = new Utf8JsonWriter(stream, WriterOptions))
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("schema", CurrentSchema);
                    writer.WriteStartArray("searches");
                    foreach (var item in _items)
                    {
                        writer.WriteStartObject();
                        writer.WriteString("name", item.Name);
                        writer.WriteString("query", item.Query);
                        writer.WriteEndObject();
                    }

                    writer.WriteEndArray();
                    writer.WriteEndObject();
                }

                stream.WriteByte((byte)'\n');
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            try
            {
                File.Delete(temporary);
            }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
            {
                // Best effort: the original error is the one worth reporting.
            }

            throw new SavedSearchException($"{path}: cannot write file: {e.Message}", innerException: e);
        }
    }

    public SavedSearch? Find(string name) =>
        _items.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Adds the search, or with <paramref name="replace"/> replaces the one with this name.</summary>
    /// <exception cref="SavedSearchException">The name or the query cannot be kept, or the name is in use and <paramref name="replace"/> is false.</exception>
    public SavedSearch Add(string name, string query, bool replace = false)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(query);
        if (!NamePattern().IsMatch(name))
        {
            throw new SavedSearchException(
                $"'{name}' is not a name for a saved search: use letters, digits, '_', '.' and '-', at most {MaxNameLength} characters, and start with a letter or a digit");
        }

        var text = query.Trim();
        if (text.Length == 0)
        {
            throw new SavedSearchException("a saved search needs a query");
        }

        if (References(text).Any())
        {
            throw new SavedSearchException("a saved search cannot use another saved search (@name)");
        }

        try
        {
            if (QueryParser.Parse(text).IsEmpty)
            {
                throw new SavedSearchException("a saved search needs a query");
            }
        }
        catch (QueryException e)
        {
            throw new SavedSearchException(e.Message, innerException: e);
        }

        var existing = Find(name);
        if (existing is not null && !replace)
        {
            throw new SavedSearchException($"a saved search named '{existing.Name}' already exists (use --replace to change it)");
        }

        var item = new SavedSearch(name, text);
        if (existing is null)
        {
            _items.Add(item);
        }
        else
        {
            _items[_items.IndexOf(existing)] = item;
        }

        return item;
    }

    /// <summary>Removes the search; false if there is none with this name.</summary>
    public bool Remove(string name) => _items.RemoveAll(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)) > 0;

    /// <summary>
    /// The query with every <c>@name</c> (a word that starts with @ outside quotation marks) replaced by the saved query. The file is
    /// read only if the query has such a word.
    /// </summary>
    /// <param name="load">Gives the saved searches; called at most once.</param>
    /// <exception cref="QueryException">A name is not saved, or the file cannot be read.</exception>
    public static string Expand(string text, Func<SavedSearches> load)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(load);
        var references = References(text).ToList();
        if (references.Count == 0)
        {
            return text;
        }

        SavedSearches set;
        try
        {
            set = load();
        }
        catch (SavedSearchException e)
        {
            throw new QueryException(e.Message);
        }

        var result = new StringBuilder();
        var from = 0;
        foreach (var (start, length, name) in references)
        {
            var found = set.Find(name) ?? throw new QueryException($"no saved search '{name}' (see shelf searches list)");
            result.Append(text, from, start - from).Append(found.Query);
            from = start + length;
        }

        return result.Append(text, from, text.Length - from).ToString();
    }

    /// <summary>The words of the form <c>@name</c> outside quotation marks: where they start, how long they are and the name.</summary>
    private static IEnumerable<(int Start, int Length, string Name)> References(string text)
    {
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '"')
            {
                quoted = !quoted;
                continue;
            }

            if (quoted || c != '@' || (i > 0 && !char.IsWhiteSpace(text[i - 1])))
            {
                continue;
            }

            var end = i + 1;
            while (end < text.Length && !char.IsWhiteSpace(text[end]) && text[end] != '"')
            {
                end++;
            }

            var name = text[(i + 1)..end];
            if (name.Length > 0 && NamePattern().IsMatch(name) && (end == text.Length || char.IsWhiteSpace(text[end])))
            {
                yield return (i, end - i, name);
            }

            i = end - 1;
        }
    }

    [GeneratedRegex(@"^[\p{L}\p{N}][\p{L}\p{N}_.\-]{0,39}$")]
    private static partial Regex NamePattern();
}
