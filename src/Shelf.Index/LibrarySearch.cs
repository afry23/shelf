using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Shelf.Core.Model;
using Shelf.Core.Search;

namespace Shelf.Index;

/// <summary>A place in a file where the words were found.</summary>
/// <param name="File">Library-relative path.</param>
/// <param name="Locator"><c>page=N</c> or an EPUB CFI.</param>
/// <param name="Where">The locator for people: "page 12", "section 3".</param>
/// <param name="Snippet">The text around the hit; each matching word in [square brackets].</param>
public sealed record ContentHit(string File, string Format, string Locator, string Where, string Snippet);

/// <summary>A work that matches, best first.</summary>
/// <param name="Score">Higher is better; 0 for a search that only filters.</param>
/// <param name="Hits">The best places in the text, at most the number asked for.</param>
public sealed record SearchHit(
    Ulid WorkId,
    string Title,
    IReadOnlyList<string> Authors,
    int? Year,
    string? Language,
    IReadOnlyList<string> Formats,
    double Score,
    IReadOnlyList<ContentHit> Hits);

/// <summary>
/// Full-text search over metadata and content (F-SRC-1, F-SRC-2, F-SRC-3, F-SRC-5) with SQLite FTS5.
/// </summary>
/// <remarks>
/// A work matches if all words are in its metadata (title, subtitle, authors, description, tags) <b>or</b> all
/// words are together in one passage of its text; a word in the title and another only in the text does not
/// match. The score adds the BM25 of both parts: the metadata columns are weighted (title 10, authors 8, subtitle 5,
/// tags 3, description 1; ADR 0004), so a title hit ranks above a hit in the text. Filters and exclusions act on
/// works: <c>-word</c> removes every work that has the word in its metadata or anywhere in its text. Words are
/// always passed to FTS5 as quoted phrases, so no character of the query is syntax.
/// </remarks>
public static class LibrarySearch
{
    public const int DefaultLimit = 20;
    public const int DefaultHitsPerWork = 3;

    // Candidates examined per source; far more than a person reads, bounded so that a one-letter query stays fast.
    private const int MaxCandidates = 500;

    private const string MetadataRank = "bm25(works_fts, 10.0, 5.0, 8.0, 1.0, 3.0)";

    public static List<SearchHit> Search(SqliteConnection connection, SearchQuery query, int limit = DefaultLimit, int hitsPerWork = DefaultHitsPerWork)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(hitsPerWork);

        var filter = BuildFilter(query);
        var ranked = query.Include.Count > 0
            ? RankByText(connection, query, filter, limit, hitsPerWork)
            : ListByFilter(connection, filter, limit);
        return Load(connection, ranked);
    }

    // ---- ranking ----

    private sealed record Ranked(string WorkId, double Score, List<ContentHit> Hits);

    private static List<Ranked> RankByText(SqliteConnection connection, SearchQuery query, Filter filter, int limit, int hitsPerWork)
    {
        var match = string.Join(' ', query.Include.Select(Phrase));
        var metadata = new Dictionary<string, double>(StringComparer.Ordinal);
        var content = new Dictionary<string, List<(double Score, long ChunkId, ContentHit Hit)>>(StringComparer.Ordinal);

        using (var command = connection.CreateCommand())
        {
            command.CommandText = $"""
                SELECT w.id, {MetadataRank}
                FROM works_fts JOIN works w ON w.seq = works_fts.rowid
                WHERE works_fts MATCH $match {filter.AndSql}
                ORDER BY 2 LIMIT {MaxCandidates}
                """;
            command.Parameters.AddWithValue("$match", match);
            filter.Bind(command);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                metadata[reader.GetString(0)] = reader.GetDouble(1);
            }
        }

        {
            using var command = connection.CreateCommand();
            command.CommandText = $"""
                SELECT w.id, bm25(chunks_fts), c.id, f.rel_path, f.format, c.locator
                FROM chunks_fts
                JOIN chunks c ON c.id = chunks_fts.rowid
                JOIN files f ON f.id = c.file_id
                JOIN works w ON w.id = f.work_id
                WHERE chunks_fts MATCH $match {filter.AndSql}
                ORDER BY 2 LIMIT {MaxCandidates}
                """;
            command.Parameters.AddWithValue("$match", match);
            filter.Bind(command);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var work = reader.GetString(0);
                if (!content.TryGetValue(work, out var hits))
                {
                    content[work] = hits = [];
                }

                // The snippet is cut out later, for the hits that are shown: it costs much more than the ranking.
                var locator = reader.GetString(5);
                hits.Add((
                    reader.GetDouble(1),
                    reader.GetInt64(2),
                    new ContentHit(reader.GetString(3), reader.GetString(4), locator, Locators.Describe(locator), string.Empty)));
            }
        }

        // bm25() is negative and smaller is better: the score turns that around and adds both parts.
        var top = metadata.Keys.Union(content.Keys)
            .Select(id =>
            {
                var best = content.TryGetValue(id, out var h) ? h.Min(x => x.Score) : 0;
                var score = -(metadata.GetValueOrDefault(id)) - best;
                var hits = h?.OrderBy(x => x.Score).Take(hitsPerWork).ToList() ?? [];
                return (Id: id, Score: score, Hits: hits);
            })
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.Id, StringComparer.Ordinal)
            .Take(limit)
            .ToList();

        using var snippet = connection.CreateCommand();
        snippet.CommandText = "SELECT snippet(chunks_fts, 0, '[', ']', '…', 12) FROM chunks_fts WHERE chunks_fts MATCH $match AND rowid = $id";
        snippet.Parameters.AddWithValue("$match", match);
        var idParameter = snippet.Parameters.Add("$id", SqliteType.Integer);
        return [.. top.Select(r => new Ranked(
            r.Id,
            r.Score,
            [.. r.Hits.Select(h =>
            {
                idParameter.Value = h.ChunkId;
                var text = (string?)snippet.ExecuteScalar() ?? string.Empty;
                return h.Hit with { Snippet = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)) };
            })]))];
    }

    private static List<Ranked> ListByFilter(SqliteConnection connection, Filter filter, int limit)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT w.id FROM works w WHERE 1 = 1 {filter.AndSql} ORDER BY w.author_sort, w.title_norm, w.id LIMIT {limit}";
        filter.Bind(command);
        using var reader = command.ExecuteReader();
        var result = new List<Ranked>();
        while (reader.Read())
        {
            result.Add(new Ranked(reader.GetString(0), 0, []));
        }

        return result;
    }

    // ---- filters ----

    /// <summary>Conditions on the work (alias <c>w</c>) with their parameters.</summary>
    private sealed class Filter
    {
        private readonly List<string> _conditions = [];
        private readonly List<(string Name, object Value)> _parameters = [];

        public string AndSql => _conditions.Count == 0 ? string.Empty : " AND " + string.Join(" AND ", _conditions);

        public void Add(string condition, params object[] values)
        {
            // Parameter names are numbered here; "{0}" in the condition is replaced by the first name, and so on.
            var names = values.Select(v =>
            {
                var name = "$f" + _parameters.Count.ToString(CultureInfo.InvariantCulture);
                _parameters.Add((name, v));
                return name;
            }).ToArray();
            _conditions.Add(string.Format(CultureInfo.InvariantCulture, condition, [.. names]));
        }

        public void Bind(SqliteCommand command)
        {
            foreach (var (name, value) in _parameters)
            {
                command.Parameters.AddWithValue(name, value);
            }
        }
    }

    private static Filter BuildFilter(SearchQuery query)
    {
        var filter = new Filter();
        foreach (var f in query.Filters)
        {
            var not = f.Negated ? "NOT " : string.Empty;
            switch (f)
            {
                case TextFilter text:
                    var columns = text.Field switch
                    {
                        FieldKind.Author => "authors",
                        FieldKind.Title => "{title subtitle}",
                        _ => "tags",
                    };
                    filter.Add($"w.seq {not}IN (SELECT rowid FROM works_fts WHERE works_fts MATCH {{0}})", $"{columns} : {Phrase(text.Term)}");
                    break;
                case ValueFilter { Field: FieldKind.Language } lang:
                    // "en" also means "en-GB"
                    filter.Add($"{not}(lower(w.language) = {{0}} OR lower(w.language) LIKE {{1}})", lang.Value, lang.Value + "-%");
                    break;
                case ValueFilter { Field: FieldKind.Format } format:
                    filter.Add($"{not}EXISTS (SELECT 1 FROM files fx WHERE fx.work_id = w.id AND fx.format = {{0}})", format.Value);
                    break;
                case ValueFilter status:
                    filter.Add($"w.read_status {(f.Negated ? "<>" : "=")} {{0}}", status.Value);
                    break;
                case YearFilter year:
                    var bounds = new List<string>();
                    var values = new List<object>();
                    if (year.Min is { } min)
                    {
                        bounds.Add($"w.year >= {{{values.Count}}}");
                        values.Add(min);
                    }

                    if (year.Max is { } max)
                    {
                        bounds.Add($"w.year <= {{{values.Count}}}");
                        values.Add(max);
                    }

                    // A work without a year matches no year filter, and is not removed by a negated one.
                    filter.Add(f.Negated ? $"NOT COALESCE({string.Join(" AND ", bounds)}, 0)" : string.Join(" AND ", bounds), [.. values]);
                    break;
            }
        }

        if (query.Exclude.Count > 0)
        {
            var any = string.Join(" OR ", query.Exclude.Select(Phrase));
            filter.Add("w.seq NOT IN (SELECT rowid FROM works_fts WHERE works_fts MATCH {0})", any);
            filter.Add(
                "w.id NOT IN (SELECT fe.work_id FROM chunks_fts JOIN chunks ce ON ce.id = chunks_fts.rowid JOIN files fe ON fe.id = ce.file_id WHERE chunks_fts MATCH {0})",
                any);
        }

        return filter;
    }

    /// <summary>A term as an FTS5 phrase: always quoted (so no character is syntax), with a star after it for a prefix.</summary>
    private static string Phrase(SearchTerm term) => "\"" + term.Text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" + (term.Prefix ? "*" : string.Empty);

    // ---- result data ----

    private static List<SearchHit> Load(SqliteConnection connection, List<Ranked> ranked)
    {
        if (ranked.Count == 0)
        {
            return [];
        }

        var ids = ranked.Select(r => r.WorkId).ToList();
        var placeholders = string.Join(", ", ids.Select((_, i) => "$i" + i.ToString(CultureInfo.InvariantCulture)));
        var works = new Dictionary<string, (string Title, List<string> Authors, int? Year, string? Language)>(StringComparer.Ordinal);
        var formats = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);

        using (var command = connection.CreateCommand())
        {
            command.CommandText = $"SELECT id, title, authors_json, year, language FROM works WHERE id IN ({placeholders})";
            AddIds(command, ids);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                using var authors = JsonDocument.Parse(reader.GetString(2));
                works[reader.GetString(0)] = (
                    reader.GetString(1),
                    [.. authors.RootElement.EnumerateArray().Select(a => a.GetString()!)],
                    reader.IsDBNull(3) ? null : reader.GetInt32(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4));
            }
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = $"SELECT DISTINCT work_id, format FROM files WHERE work_id IN ({placeholders})";
            AddIds(command, ids);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var work = reader.GetString(0);
                if (!formats.TryGetValue(work, out var set))
                {
                    formats[work] = set = new SortedSet<string>(StringComparer.Ordinal);
                }

                set.Add(reader.GetString(1));
            }
        }

        return [.. ranked.Select(r =>
        {
            var (title, authors, year, language) = works[r.WorkId];
            return new SearchHit(Ulid.Parse(r.WorkId), title, authors, year, language, [.. formats.GetValueOrDefault(r.WorkId) ?? []], r.Score, r.Hits);
        })];
    }

    private static void AddIds(SqliteCommand command, List<string> ids)
    {
        for (var i = 0; i < ids.Count; i++)
        {
            command.Parameters.AddWithValue("$i" + i.ToString(CultureInfo.InvariantCulture), ids[i]);
        }
    }
}
