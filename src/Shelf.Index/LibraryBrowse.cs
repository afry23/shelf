using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Shelf.Core.Grouping;
using Shelf.Core.Model;
using Shelf.Core.Search;

namespace Shelf.Index;

public enum WorkSort
{
    Title,
    Author,
    Year,
    Added,
    Updated,

    /// <summary>When the work was put on the reading list (F-RLS-4); works that are not on it go last.</summary>
    ReadingList,
}

/// <param name="Offset">Works to skip.</param>
/// <param name="Limit">Works to return.</param>
public sealed record BrowseOptions(WorkSort Sort = WorkSort.Title, bool Descending = false, int Offset = 0, int Limit = BrowseOptions.DefaultLimit)
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 100;
}

/// <summary>A work as the library view shows it.</summary>
public sealed record WorkListEntry(
    Ulid Id,
    string Title,
    string? Subtitle,
    IReadOnlyList<string> Authors,
    int? Year,
    string? Language,
    IReadOnlyList<string> Formats,
    IReadOnlyList<string> Tags,
    string ReadStatus,
    bool HasCover,
    string? ReadingListAt = null);

/// <summary>A value of a facet and the number of works of the filtered set that have it.</summary>
public sealed record FacetCount(string Value, int Count);

/// <summary>Counts over the filtered set, for the filter panel. Tags, authors, publishers and series are cut at <see cref="LibraryBrowse.FacetLimit"/> values.</summary>
public sealed record BrowseFacets(
    IReadOnlyList<FacetCount> Tags,
    IReadOnlyList<FacetCount> Authors,
    IReadOnlyList<FacetCount> Publishers,
    IReadOnlyList<FacetCount> Languages,
    IReadOnlyList<FacetCount> Years,
    IReadOnlyList<FacetCount> Formats,
    IReadOnlyList<FacetCount> Statuses,
    IReadOnlyList<FacetCount> Series,
    IReadOnlyList<FacetCount> Types);

/// <param name="Total">Works that pass the filter, whatever the page.</param>
public sealed record BrowsePage(int Total, IReadOnlyList<WorkListEntry> Items, BrowseFacets Facets);

/// <summary>
/// The library as a list (PRD 8.2, library view): the works that pass the filters of the query language, in an order,
/// one page of them, and counts per facet over all the works that pass (not only the page).
/// </summary>
public static class LibraryBrowse
{
    public const int FacetLimit = 50;

    /// <param name="query">Filters (and exclusions) only; words are the job of <see cref="LibrarySearch"/>.</param>
    public static BrowsePage Browse(SqliteConnection connection, SearchQuery query, BrowseOptions options)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(options);
        if (query.Include.Count > 0)
        {
            throw new ArgumentException("A list takes filters only; use LibrarySearch for words.", nameof(query));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(options.Offset);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.Limit, 1);

        var filter = LibrarySearch.BuildFilter(query);
        var selection = $"SELECT w.id FROM works w WHERE 1 = 1 {filter.AndSql}";

        using var count = Command(connection, $"SELECT COUNT(*) FROM ({selection})", filter);
        var total = Convert.ToInt32(count.ExecuteScalar(), CultureInfo.InvariantCulture);

        return new BrowsePage(total, Page(connection, filter, options), Facets(connection, filter, selection));
    }

    private static List<WorkListEntry> Page(SqliteConnection connection, LibrarySearch.Filter filter, BrowseOptions options)
    {
        var ids = options.Sort == WorkSort.Author ? AuthorOrder(connection, filter, options) : SqlOrder(connection, filter, options);
        if (ids.Count == 0)
        {
            return [];
        }

        var rows = new Dictionary<string, (string Title, string? Subtitle, List<string> Authors, int? Year, string? Language, string Status, bool HasCover, string? ReadingListAt)>(StringComparer.Ordinal);
        using (var command = connection.CreateCommand())
        {
            command.CommandText = $"SELECT w.id, w.title, w.subtitle, w.authors_json, w.year, w.language, w.read_status, w.cover_path IS NOT NULL, w.reading_list_at FROM works w WHERE w.id IN ({Placeholders(command, ids)})";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                rows[reader.GetString(0)] = (
                    reader.GetString(1),
                    reader.IsDBNull(2) ? null : reader.GetString(2),
                    Names(reader.GetString(3)),
                    reader.IsDBNull(4) ? null : reader.GetInt32(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5),
                    reader.GetString(6),
                    reader.GetBoolean(7),
                    reader.IsDBNull(8) ? null : reader.GetString(8));
            }
        }

        var tags = Grouped(connection, ids, "SELECT wt.work_id, t.path FROM work_tags wt JOIN tags t ON t.id = wt.tag_id WHERE wt.work_id IN ({0}) ORDER BY t.path");
        var formats = Grouped(connection, ids, "SELECT DISTINCT work_id, format FROM files WHERE work_id IN ({0}) ORDER BY format");
        return [.. ids.Select(id =>
        {
            var r = rows[id];
            return new WorkListEntry(Ulid.Parse(id), r.Title, r.Subtitle, r.Authors, r.Year, r.Language, formats.GetValueOrDefault(id) ?? [], tags.GetValueOrDefault(id) ?? [], r.Status, r.HasCover, r.ReadingListAt);
        })];
    }

    private static List<string> SqlOrder(SqliteConnection connection, LibrarySearch.Filter filter, BrowseOptions options)
    {
        var direction = options.Descending ? "DESC" : "ASC";
        var order = options.Sort switch
        {
            WorkSort.Title => $"w.title_norm {direction}, w.id",

            // A work without a year goes last in either direction.
            WorkSort.Year => $"w.year IS NULL, w.year {direction}, w.title_norm, w.id",
            WorkSort.ReadingList => $"w.reading_list_at IS NULL, w.reading_list_at {direction}, w.title_norm, w.id",
            WorkSort.Added => $"w.added_at IS NULL, w.added_at {direction}, w.title_norm, w.id",
            _ => $"w.updated_at {direction}, w.title_norm, w.id",
        };

        using var command = Command(connection, $"SELECT w.id FROM works w WHERE 1 = 1 {filter.AndSql} ORDER BY {order} LIMIT $limit OFFSET $offset", filter);
        command.Parameters.AddWithValue("$limit", options.Limit);
        command.Parameters.AddWithValue("$offset", options.Offset);
        using var reader = command.ExecuteReader();
        var ids = new List<string>();
        while (reader.Read())
        {
            ids.Add(reader.GetString(0));
        }

        return ids;
    }

    // The sort key of an author keeps its accents ("Ålander" would come after "Zimmer" in SQLite's byte order), so this order
    // is made here, on the folded key. It reads one short key per work of the filtered set, not the works themselves.
    private static List<string> AuthorOrder(SqliteConnection connection, LibrarySearch.Filter filter, BrowseOptions options)
    {
        var keys = new List<(string Id, string Author, string Title)>();
        using (var command = Command(connection, $"SELECT w.id, w.author_sort, w.title_norm FROM works w WHERE 1 = 1 {filter.AndSql}", filter))
        {
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                keys.Add((reader.GetString(0), TextFolding.Fold(reader.GetString(1)), reader.GetString(2)));
            }
        }

        var ordered = options.Descending
            ? keys.OrderByDescending(k => k.Author, StringComparer.Ordinal)
            : keys.OrderBy(k => k.Author, StringComparer.Ordinal);
        return [.. ordered
            .ThenBy(k => k.Title, StringComparer.Ordinal)
            .ThenBy(k => k.Id, StringComparer.Ordinal)
            .Skip(options.Offset)
            .Take(options.Limit)
            .Select(k => k.Id)];
    }

    private static BrowseFacets Facets(SqliteConnection connection, LibrarySearch.Filter filter, string selection)
    {
        var tags = Counts(connection, filter, $"SELECT t.path, COUNT(*) FROM work_tags wt JOIN tags t ON t.id = wt.tag_id WHERE wt.work_id IN ({selection}) GROUP BY t.id ORDER BY 2 DESC, 1 LIMIT {FacetLimit}");
        var languages = Counts(connection, filter, $"SELECT lower(w.language), COUNT(*) FROM works w WHERE w.id IN ({selection}) AND w.language IS NOT NULL GROUP BY 1 ORDER BY 2 DESC, 1");
        var publishers = Counts(connection, filter, $"SELECT w.publisher, COUNT(*) FROM works w WHERE w.id IN ({selection}) AND w.publisher IS NOT NULL GROUP BY 1 ORDER BY 2 DESC, 1 LIMIT {FacetLimit}");
        var years = Counts(connection, filter, $"SELECT w.year, COUNT(*) FROM works w WHERE w.id IN ({selection}) AND w.year IS NOT NULL GROUP BY 1 ORDER BY 1 DESC");
        var formats = Counts(connection, filter, $"SELECT f.format, COUNT(DISTINCT f.work_id) FROM files f WHERE f.work_id IN ({selection}) GROUP BY f.format ORDER BY 2 DESC, 1");
        var statuses = Counts(connection, filter, $"SELECT w.read_status, COUNT(*) FROM works w WHERE w.id IN ({selection}) GROUP BY 1 ORDER BY 2 DESC, 1");

        // A series written in two cases is one series (the filter ignores case); the spelling that sorts first is shown.
        var series = Counts(connection, filter, $"SELECT MIN(w.series), COUNT(*) FROM works w WHERE w.id IN ({selection}) AND w.series IS NOT NULL GROUP BY lower(w.series) ORDER BY 2 DESC, 1 LIMIT {FacetLimit}");
        var types = Counts(connection, filter, $"SELECT w.kind, COUNT(*) FROM works w WHERE w.id IN ({selection}) GROUP BY 1 ORDER BY 2 DESC, 1");

        // Authors are in a JSON column of the work; counting them here keeps the schema as it is.
        var authors = new Dictionary<string, int>(StringComparer.Ordinal);
        using (var command = Command(connection, $"SELECT w.authors_json FROM works w WHERE w.id IN ({selection})", filter))
        {
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                foreach (var name in Names(reader.GetString(0)).Distinct(StringComparer.Ordinal))
                {
                    authors[name] = authors.GetValueOrDefault(name) + 1;
                }
            }
        }

        return new BrowseFacets(
            tags,
            [.. authors.OrderByDescending(a => a.Value).ThenBy(a => a.Key, StringComparer.Ordinal).Take(FacetLimit).Select(a => new FacetCount(a.Key, a.Value))],
            publishers,
            languages,
            years,
            formats,
            statuses,
            series,
            types);
    }

    private static List<FacetCount> Counts(SqliteConnection connection, LibrarySearch.Filter filter, string sql)
    {
        using var command = Command(connection, sql, filter);
        using var reader = command.ExecuteReader();
        var counts = new List<FacetCount>();
        while (reader.Read())
        {
            counts.Add(new FacetCount(Convert.ToString(reader.GetValue(0), CultureInfo.InvariantCulture)!, reader.GetInt32(1)));
        }

        return counts;
    }

    private static Dictionary<string, List<string>> Grouped(SqliteConnection connection, List<string> ids, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = string.Format(CultureInfo.InvariantCulture, sql, Placeholders(command, ids));
        var grouped = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var id = reader.GetString(0);
            if (!grouped.TryGetValue(id, out var list))
            {
                grouped[id] = list = [];
            }

            list.Add(reader.GetString(1));
        }

        return grouped;
    }

    /// <summary>Adds one parameter per ID and returns their names, comma separated.</summary>
    private static string Placeholders(SqliteCommand command, List<string> ids)
    {
        var names = new List<string>();
        for (var i = 0; i < ids.Count; i++)
        {
            var name = "$i" + i.ToString(CultureInfo.InvariantCulture);
            names.Add(name);
            command.Parameters.AddWithValue(name, ids[i]);
        }

        return string.Join(", ", names);
    }

    private static SqliteCommand Command(SqliteConnection connection, string sql, LibrarySearch.Filter filter)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        filter.Bind(command);
        return command;
    }

    private static List<string> Names(string json)
    {
        using var document = JsonDocument.Parse(json);
        return [.. document.RootElement.EnumerateArray().Select(a => a.GetString()!)];
    }
}
