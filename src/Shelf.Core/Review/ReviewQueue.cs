using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Shelf.Core.Model;

namespace Shelf.Core.Review;

/// <summary>Why an entry is in the review queue (F-IMP-7).</summary>
public enum ReviewKind
{
    /// <summary>A file may belong to an existing work, but not surely enough to merge it.</summary>
    Grouping,

    /// <summary>A file could not be read.</summary>
    ExtractionError,

    /// <summary>Title or authors come from the file name, or are missing.</summary>
    WeakMetadata,
}

/// <summary>A work that a file may belong to, with the evidence.</summary>
public sealed record ReviewCandidate(Ulid Work, double Confidence, IReadOnlyList<string> Signals);

/// <summary>One thing that needs a decision. Entries are removed when they are resolved.</summary>
public sealed record ReviewEntry(
    Ulid Id,
    ReviewKind Kind,
    DateTimeOffset CreatedAt,
    string Message,
    string? File = null,
    Ulid? Work = null,
    IReadOnlyList<ReviewCandidate>? Candidates = null);

/// <summary><c>.shelf/review.json</c> is damaged, newer than this version, or cannot be written.</summary>
public sealed class ReviewQueueException(string message, string sourceName, Exception? innerException = null)
    : Exception(message, innerException)
{
    public string SourceName { get; } = sourceName;
}

/// <summary>
/// The review queue of a library: <c>.shelf/review.json</c> (schema 1). It is the source of truth; the index
/// mirrors it and a reindex does not lose it. Changes replace the file atomically. Two processes that change the
/// queue at the same moment can overwrite each other's change: import and review are meant to run one at a time.
/// </summary>
public sealed class ReviewQueue
{
    public const string FileName = "review.json";
    public const int CurrentSchema = 1;

    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly List<ReviewEntry> _entries;

    public ReviewQueue(IEnumerable<ReviewEntry>? entries = null) => _entries = [.. entries ?? []];

    public IReadOnlyList<ReviewEntry> Entries => _entries;

    public static string PathFor(string libraryRoot) => Path.Combine(libraryRoot, ".shelf", FileName);

    public void Add(ReviewEntry entry) => _entries.Add(entry);

    /// <summary>Removes the entry; false if there is none with this ID.</summary>
    public bool Remove(Ulid id) => _entries.RemoveAll(e => e.Id == id) > 0;

    /// <summary>Reads the queue. A missing file is an empty queue.</summary>
    /// <exception cref="ReviewQueueException"/>
    public static ReviewQueue Load(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        string text;
        try
        {
            text = File.ReadAllText(path, new UTF8Encoding(false, throwOnInvalidBytes: true));
        }
        catch (FileNotFoundException)
        {
            return new ReviewQueue();
        }
        catch (DirectoryNotFoundException)
        {
            return new ReviewQueue();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or DecoderFallbackException)
        {
            throw new ReviewQueueException($"{path}: cannot read file: {e.Message}", path, e);
        }

        return Parse(text, path);
    }

    /// <exception cref="ReviewQueueException"/>
    public static ReviewQueue Parse(string text, string source)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text);
        }
        catch (JsonException e)
        {
            throw new ReviewQueueException($"{source}: not valid JSON: {e.Message}", source, e);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new ReviewQueueException($"{source}: not valid JSON: expected an object, found {root.ValueKind}", source);
            }

            var schema = root.TryGetProperty("schema", out var schemaElement) && schemaElement.ValueKind == JsonValueKind.Number && schemaElement.TryGetInt32(out var number)
                ? number
                : 0;
            if (schema > CurrentSchema)
            {
                throw new ReviewQueueException(
                    $"{source}: schema {schema} is newer than this version of shelf supports ({CurrentSchema})",
                    source);
            }

            if (schema < 1)
            {
                throw new ReviewQueueException($"{source}: schema: must be 1 or greater", source);
            }

            if (!root.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Array)
            {
                throw new ReviewQueueException($"{source}: entries: missing", source);
            }

            var queue = new ReviewQueue();
            var index = 0;
            foreach (var item in entries.EnumerateArray())
            {
                queue.Add(ReadEntry(item, $"entries[{++index}]", source));
            }

            return queue;
        }
    }

    /// <summary>Writes the queue: indented, LF line endings, final newline; the file is replaced atomically.</summary>
    /// <exception cref="ReviewQueueException"/>
    public void Save(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var bytes = new UTF8Encoding(false).GetBytes(Serialize());
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
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

            throw new ReviewQueueException($"{path}: cannot write file: {e.Message}", path, e);
        }
    }

    /// <summary>Adds an entry to the queue file and saves it.</summary>
    public static void Append(string path, ReviewEntry entry)
    {
        var queue = Load(path);
        queue.Add(entry);
        queue.Save(path);
    }

    /// <summary>Removes an entry from the queue file.</summary>
    /// <exception cref="ReviewQueueException">There is no entry with this ID.</exception>
    public static void Remove(string path, Ulid id)
    {
        var queue = Load(path);
        if (!queue.Remove(id))
        {
            throw new ReviewQueueException($"{path}: no review entry {id}", path);
        }

        queue.Save(path);
    }

    public string Serialize()
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schema", CurrentSchema);
            writer.WriteStartArray("entries");
            foreach (var entry in _entries)
            {
                WriteEntry(writer, entry);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length) + "\n";
    }

    /// <summary>One entry as a single-line JSON object (the payload that the index mirrors).</summary>
    public static string EntryToJson(ReviewEntry entry)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            WriteEntry(writer, entry);
        }

        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    public static string KindName(ReviewKind kind) => kind switch
    {
        ReviewKind.Grouping => "grouping",
        ReviewKind.ExtractionError => "extraction-error",
        _ => "weak-metadata",
    };

    public static ReviewKind ParseKind(string name) => name switch
    {
        "grouping" => ReviewKind.Grouping,
        "extraction-error" => ReviewKind.ExtractionError,
        "weak-metadata" => ReviewKind.WeakMetadata,
        _ => throw new FormatException($"'{name}' is not a review kind."),
    };

    public static string FormatTime(DateTimeOffset time) =>
        time.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture);

    private static void WriteEntry(Utf8JsonWriter writer, ReviewEntry entry)
    {
        writer.WriteStartObject();
        writer.WriteString("id", entry.Id.ToString());
        writer.WriteString("kind", KindName(entry.Kind));
        writer.WriteString("createdAt", FormatTime(entry.CreatedAt));
        writer.WriteString("message", entry.Message);
        if (entry.File is not null)
        {
            writer.WriteString("file", entry.File);
        }

        if (entry.Work is { } work)
        {
            writer.WriteString("work", work.ToString());
        }

        if (entry.Candidates is { Count: > 0 } candidates)
        {
            writer.WriteStartArray("candidates");
            foreach (var candidate in candidates)
            {
                writer.WriteStartObject();
                writer.WriteString("work", candidate.Work.ToString());
                writer.WriteNumber("confidence", candidate.Confidence);
                writer.WriteStartArray("signals");
                foreach (var signal in candidate.Signals)
                {
                    writer.WriteStringValue(signal);
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }

    private static ReviewEntry ReadEntry(JsonElement item, string key, string source)
    {
        ReviewQueueException Fail(string where, string problem) => new($"{source}: {where}: {problem}", source);

        if (item.ValueKind != JsonValueKind.Object)
        {
            throw Fail(key, $"expected object, found {item.ValueKind}");
        }

        string? Str(JsonElement obj, string name, string prefix, bool required)
        {
            if (!obj.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
            {
                return required ? throw Fail(prefix + name, "missing") : null;
            }

            return value.ValueKind == JsonValueKind.String ? value.GetString() : throw Fail(prefix + name, $"expected string, found {value.ValueKind}");
        }

        Ulid Id(string? text, string where) => Ulid.TryParse(text, out var id) ? id : throw Fail(where, "not a valid ULID");

        var prefix = key + ".";
        var entryId = Id(Str(item, "id", prefix, required: false), prefix + "id");
        var kindName = Str(item, "kind", prefix, required: true)!;
        ReviewKind kind;
        try
        {
            kind = ParseKind(kindName);
        }
        catch (FormatException)
        {
            throw Fail(prefix + "kind", "must be one of grouping, extraction-error, weak-metadata");
        }

        var createdText = Str(item, "createdAt", prefix, required: true)!;
        if (!DateTimeOffset.TryParseExact(
                createdText,
                "yyyy-MM-dd'T'HH:mm:ss'Z'",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out var created))
        {
            throw Fail(prefix + "createdAt", "must be a UTC time like 2026-10-12T19:04:00Z");
        }

        var message = Str(item, "message", prefix, required: true)!;
        var file = Str(item, "file", prefix, required: false);
        var workText = Str(item, "work", prefix, required: false);
        Ulid? work = workText is null ? null : Id(workText, prefix + "work");

        List<ReviewCandidate>? candidates = null;
        if (item.TryGetProperty("candidates", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            candidates = [];
            var index = 0;
            foreach (var c in list.EnumerateArray())
            {
                var where = $"{prefix}candidates[{++index}].";
                var candidateWork = Id(Str(c, "work", where, required: true), where + "work");
                if (!c.TryGetProperty("confidence", out var confidence) || confidence.ValueKind != JsonValueKind.Number)
                {
                    throw Fail(where + "confidence", "missing");
                }

                var signals = c.TryGetProperty("signals", out var s) && s.ValueKind == JsonValueKind.Array
                    ? s.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToList()
                    : [];
                candidates.Add(new ReviewCandidate(candidateWork, confidence.GetDouble(), signals));
            }
        }

        return new ReviewEntry(entryId, kind, created, message, file, work, candidates);
    }
}
