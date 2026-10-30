using System.Globalization;
using System.Text;
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

    private const string MetadataRank = "bm25(works_fts, 10.0, 5.0, 8.0, 1.0, 3.0, 2.0, 2.0, 1.0)";

    // The columns that hold the text as written; the stem columns are only reached by the stemmed form of a word.
    private const string ExactColumns = "{title subtitle authors description tags}";

    /// <summary>
    /// Searches the library. With <paramref name="titleOnly"/> only the title and the subtitle are looked at (F-SRC-7): the
    /// words of the query must be in them, the text of the books, the authors, the tags and the description do not count,
    /// and a hit has no places in the text. Filters and exclusions still act on the whole work.
    /// </summary>
    public static List<SearchHit> Search(SqliteConnection connection, SearchQuery query, int limit = DefaultLimit, int hitsPerWork = DefaultHitsPerWork, bool titleOnly = false)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(hitsPerWork);

        var filter = BuildFilter(query);
        var ranked = query.Include.Count > 0
            ? (titleOnly ? RankByTitle(connection, query, filter, limit) : RankByText(connection, query, filter, limit, hitsPerWork))
            : ListByFilter(connection, filter, limit);
        return Load(connection, ranked);
    }

    // ---- ranking ----

    private sealed record Ranked(string WorkId, double Score, List<ContentHit> Hits);

    private static List<Ranked> RankByTitle(SqliteConnection connection, SearchQuery query, Filter filter, int limit)
    {
        var candidates = new List<(string Id, double Rank)>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = $"""
                SELECT w.id, {MetadataRank}
                FROM works_fts JOIN works w ON w.seq = works_fts.rowid
                WHERE works_fts MATCH $match {filter.AndSql}
                ORDER BY 2 LIMIT {MaxCandidates}
                """;
            command.Parameters.AddWithValue("$match", string.Join(" AND ", query.Include.Select(t => TitleTerm(t))));
            filter.Bind(command);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                candidates.Add((reader.GetString(0), reader.GetDouble(1)));
            }
        }

        // The stem columns hold the stems of the description and the tags too, so a word that matched by its stem is
        // checked against the words of the title and the subtitle.
        var plain = query.Include.Where(t => !t.Quoted && !t.Prefix && t.Text.Length >= 3 && t.Text.All(char.IsLetter)).ToList();
        var titles = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        if (plain.Count > 0 && candidates.Count > 0)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT title, subtitle FROM works WHERE id = $id";
            var id = command.Parameters.Add("$id", SqliteType.Text);
            foreach (var (work, _) in candidates)
            {
                id.Value = work;
                using var reader = command.ExecuteReader();
                if (!reader.Read())
                {
                    continue;
                }

                var words = new HashSet<string>(StringComparer.Ordinal);
                foreach (var word in Stemmer.Words(reader.GetString(0)).Concat(Stemmer.Words(reader.IsDBNull(1) ? null : reader.GetString(1))))
                {
                    words.Add(Fold(word));
                    foreach (var stemmer in new[] { Stemmer.English, Stemmer.German })
                    {
                        var stem = Stemmer.Stem(word.ToLowerInvariant(), stemmer);
                        words.Add(Fold(stem));
                        words.Add(Fold(Stemmer.Stem(stem, stemmer)));
                    }
                }

                titles[work] = words;
            }
        }

        return [.. candidates
            .Where(c => plain.Count == 0 || (titles.TryGetValue(c.Id, out var words) && plain.All(t => TitleHas(words, t))))
            .Select(c => new Ranked(c.Id, -c.Rank, []))
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.WorkId, StringComparer.Ordinal)
            .Take(limit)];
    }

    // Exact in the title columns, or by stem (checked afterwards, see RankByTitle).
    private static string TitleTerm(SearchTerm term)
    {
        var exact = $"{{title subtitle}} : {Phrase(term)}";
        if (term.Quoted || term.Prefix || term.Text.Length < 3 || !term.Text.All(char.IsLetter))
        {
            return exact;
        }

        var lower = term.Text.ToLowerInvariant();
        return $"({exact} OR stems_en : {Quote(Stemmer.Stem(lower, Stemmer.English))} OR stems_de : {Quote(Stemmer.Stem(lower, Stemmer.German))})";
    }

    private static bool TitleHas(HashSet<string> words, SearchTerm term)
    {
        var lower = term.Text.ToLowerInvariant();
        return words.Contains(Fold(lower))
            || new[] { Stemmer.English, Stemmer.German }.Any(stemmer => words.Contains(Fold(Stemmer.Stem(lower, stemmer))));
    }

    private static List<Ranked> RankByText(SqliteConnection connection, SearchQuery query, Filter filter, int limit, int hitsPerWork)
    {
        var match = string.Join(' ', query.Include.Select(Phrase));
        var metadataMatch = string.Join(" AND ", query.Include.Select(MetadataTerm));
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
            command.Parameters.AddWithValue("$match", metadataMatch);
            filter.Bind(command);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                metadata[reader.GetString(0)] = reader.GetDouble(1);
            }
        }

        {
            using var command = connection.CreateCommand();
            // Without conditions on the work, FTS5 picks the best passages first and only those are joined: for a
            // common word the join of every match would cost more than the ranking. With conditions the join is
            // needed to apply them.
            command.CommandText = filter.IsEmpty
                ? $"""
                    SELECT w.id, r.score, c.id, f.rel_path, f.format, c.locator
                    FROM (SELECT rowid, bm25(chunks_fts) AS score FROM chunks_fts WHERE chunks_fts MATCH $match ORDER BY 2 LIMIT {MaxCandidates}) r
                    JOIN chunks c ON c.id = r.rowid
                    JOIN files f ON f.id = c.file_id
                    JOIN works w ON w.id = f.work_id
                    ORDER BY r.score
                    """
                : $"""
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

        // The marked text and the notes of the annotations are passages too (F-ANN-5); their ids count down from -1 so that they
        // stay apart from the ids of the chunks.
        using (var command = connection.CreateCommand())
        {
            command.CommandText = $"""
                SELECT w.id, bm25(annotations_fts), -a.id, f.rel_path, f.format, a.locator, a.kind
                FROM annotations_fts
                JOIN annotations a ON a.id = annotations_fts.rowid
                JOIN works w ON w.id = a.work_id
                JOIN files f ON f.work_id = a.work_id AND substr(f.rel_path, -length(a.file_name)) = a.file_name
                WHERE annotations_fts MATCH $match {filter.AndSql}
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

                var locator = reader.IsDBNull(5) ? string.Empty : reader.GetString(5);
                var kind = reader.GetString(6);
                var where = kind == "note" ? "note" : kind + ", " + Locators.Describe(locator);
                hits.Add((
                    reader.GetDouble(1),
                    reader.GetInt64(2),
                    new ContentHit(reader.GetString(3), reader.GetString(4), locator, where, string.Empty)));
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
        using var noteSnippet = connection.CreateCommand();
        noteSnippet.CommandText = "SELECT snippet(annotations_fts, -1, '[', ']', '…', 12) FROM annotations_fts WHERE annotations_fts MATCH $match AND rowid = $id";
        noteSnippet.Parameters.AddWithValue("$match", match);
        var noteIdParameter = noteSnippet.Parameters.Add("$id", SqliteType.Integer);
        return [.. top.Select(r => new Ranked(
            r.Id,
            r.Score,
            [.. r.Hits.Select(h =>
            {
                string? text;
                if (h.ChunkId < 0)
                {
                    noteIdParameter.Value = -h.ChunkId;
                    text = (string?)noteSnippet.ExecuteScalar();
                }
                else
                {
                    idParameter.Value = h.ChunkId;
                    text = (string?)snippet.ExecuteScalar();
                }

                text ??= string.Empty;
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
    internal sealed class Filter
    {
        private readonly List<string> _conditions = [];
        private readonly List<(string Name, object Value)> _parameters = [];

        public bool IsEmpty => _conditions.Count == 0;

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

    internal static Filter BuildFilter(SearchQuery query)
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
                        FieldKind.Publisher => "publisher",
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
                case ValueFilter { Field: FieldKind.Type } type:
                    filter.Add($"w.kind {(f.Negated ? "<>" : "=")} {{0}}", type.Value);
                    break;
                case ValueFilter { Field: FieldKind.Doi } doi:
                    filter.Add($"{not}COALESCE(w.doi = {{0}}, 0)", doi.Value);
                    break;
                case ValueFilter { Field: FieldKind.Arxiv } arxiv:
                    filter.Add($"{not}COALESCE(w.arxiv_id = {{0}}, 0)", arxiv.Value);
                    break;
                case ValueFilter { Field: FieldKind.Venue } venue:
                    // Contains, ignoring case: "venue:neurips" finds "Advances in NeurIPS 2017".
                    filter.Add($"{not}COALESCE(instr(lower(w.venue), {{0}}) > 0, 0)", venue.Value);
                    break;
                case ValueFilter { Field: FieldKind.Site } site:
                    // Contains, ignoring case: "site:ops" finds "Ops Weekly".
                    filter.Add($"{not}COALESCE(instr(lower(w.site), {{0}}) > 0, 0)", site.Value);
                    break;
                case ValueFilter { Field: FieldKind.ReadingList } onList:
                    // "yes" is a work with a time on the list; a negation turns the answer around.
                    filter.Add(onList.Value == "yes" != f.Negated ? "w.reading_list_at IS NOT NULL" : "w.reading_list_at IS NULL");
                    break;
                case ValueFilter { Field: FieldKind.Series } series:
                    // The whole name of the series, ignoring case: "series:\"reusable elements\"" finds "Reusable Elements".
                    filter.Add($"{not}COALESCE(lower(w.series) = {{0}}, 0)", series.Value);
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
            filter.Add(
                "w.seq NOT IN (SELECT rowid FROM works_fts WHERE works_fts MATCH {0})",
                string.Join(" OR ", query.Exclude.Select(t => $"{ExactColumns} : {Phrase(t)}")));
            filter.Add(
                "w.id NOT IN (SELECT fe.work_id FROM chunks_fts JOIN chunks ce ON ce.id = chunks_fts.rowid JOIN files fe ON fe.id = ce.file_id WHERE chunks_fts MATCH {0})",
                any);
        }

        return filter;
    }

    /// <summary>A term as an FTS5 phrase: always quoted (so no character is syntax), with a star after it for a prefix.</summary>
    private static string Phrase(SearchTerm term) => "\"" + term.Text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" + (term.Prefix ? "*" : string.Empty);

    /// <summary>
    /// A word for the metadata: the word as written, or, for a plain word, also its stem in the column of each
    /// language (F-SRC-6). A quoted phrase, a prefix and a word with other characters than letters stay exact.
    /// </summary>
    private static string MetadataTerm(SearchTerm term)
    {
        var exact = $"{ExactColumns} : {Phrase(term)}";
        if (term.Quoted || term.Prefix || term.Text.Length < 3 || !term.Text.All(char.IsLetter))
        {
            return exact;
        }

        var lower = term.Text.ToLowerInvariant();
        return $"({exact} OR stems_en : {Quote(Stemmer.Stem(lower, Stemmer.English))} OR stems_de : {Quote(Stemmer.Stem(lower, Stemmer.German))})";
    }

    private static string Quote(string text) => "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    // ---- fuzzy search (F-SRC-4) ----

    private const double MinSimilarity = 0.4;
    private const int MinFuzzyLength = 4;

    /// <summary>
    /// The works with a word in title, subtitle or authors that is close to <b>every</b> plain word of the query,
    /// by the overlap of trigrams, best first. For the fallback when <see cref="Search"/> finds nothing. A quoted
    /// phrase or a prefix is exact, so a query with one has no fuzzy results; with <paramref name="titleOnly"/> the authors do not count; a word of fewer than four letters must
    /// be the same word. Filters and exclusions apply as in <see cref="Search"/>.
    /// </summary>
    public static List<SearchHit> SearchFuzzy(SqliteConnection connection, SearchQuery query, int limit = DefaultLimit, bool titleOnly = false)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);

        if (query.Include.Count == 0 || query.Include.Any(t => t.Quoted || t.Prefix))
        {
            return [];
        }

        var terms = query.Include.Select(t => Fold(t.Text)).ToList();
        var grams = terms.Select(t => t.Length >= MinFuzzyLength ? Trigrams(t) : null).ToList();
        var filter = BuildFilter(query);
        var ranked = new List<Ranked>();

        using (var command = connection.CreateCommand())
        {
            command.CommandText = $"SELECT w.id, w.title, w.subtitle, w.authors_json FROM works w WHERE 1 = 1 {filter.AndSql}";
            filter.Bind(command);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var words = new HashSet<string>(StringComparer.Ordinal);
                AddWords(words, reader.GetString(1));
                AddWords(words, reader.IsDBNull(2) ? null : reader.GetString(2));
                if (!titleOnly)
                {
                    using var authors = JsonDocument.Parse(reader.GetString(3));
                    foreach (var author in authors.RootElement.EnumerateArray())
                    {
                        AddWords(words, author.GetString());
                    }
                }

                var total = 0.0;
                for (var i = 0; i < terms.Count && total >= 0; i++)
                {
                    var best = 0.0;
                    foreach (var word in words)
                    {
                        var similarity = grams[i] is { } wanted ? Similarity(wanted, word) : (word == terms[i] ? 1.0 : 0.0);
                        best = Math.Max(best, similarity);
                    }

                    total = best >= MinSimilarity ? total + best : -1;
                }

                if (total > 0)
                {
                    ranked.Add(new Ranked(reader.GetString(0), total, []));
                }
            }
        }

        return Load(connection, [.. ranked.OrderByDescending(r => r.Score).ThenBy(r => r.WorkId, StringComparer.Ordinal).Take(limit)]);
    }

    private static void AddWords(HashSet<string> words, string? text)
    {
        foreach (var word in Stemmer.Words(text))
        {
            words.Add(Fold(word));
        }
    }

    /// <summary>Lower case, without accents: the way the index compares words (ß stays ß, as in the index).</summary>
    private static string Fold(string text)
    {
        var decomposed = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                result.Append(c);
            }
        }

        return result.ToString().Normalize(NormalizationForm.FormC);
    }

    // The trigrams of " word ": three letters packed in a long.
    private static HashSet<long> Trigrams(string word)
    {
        var padded = " " + word + " ";
        var set = new HashSet<long>();
        for (var i = 0; i + 3 <= padded.Length; i++)
        {
            set.Add(((long)padded[i] << 32) | ((long)padded[i + 1] << 16) | padded[i + 2]);
        }

        return set;
    }

    private static double Similarity(HashSet<long> wanted, string word)
    {
        if (word.Length < MinFuzzyLength)
        {
            return 0;
        }

        var other = Trigrams(word);
        var common = wanted.Count(other.Contains);
        return common == 0 ? 0 : (double)common / (wanted.Count + other.Count - common);
    }

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
