using System.ComponentModel;
using System.Globalization;
using System.Text;
using Microsoft.Data.Sqlite;
using ModelContextProtocol;
using Shelf.Core.Configuration;
using Shelf.Core.Model;
using Shelf.Core.Organization;
using Shelf.Core.Search;
using Shelf.Extraction;
using Shelf.Index;
using Shelf.Organize;

namespace Shelf.Mcp;

/// <summary>
/// The read-only tools and resources of the MCP server (PRD 7.2) over one library. Errors the assistant can act on
/// (unknown work, bad query, no text at a location) are <see cref="McpException"/>s: the SDK sends their message as a
/// tool error and the server goes on. Nothing here changes a file or the index.
/// </summary>
internal sealed class LibraryTools(string root, ShelfSettings settings)
{
    public const int DefaultMaxChars = 4_000;
    public const int MaxLimit = 100;
    public const int MaxMoves = 200;

    // ---- tools ----

    [Description("Search titles, authors, tags and the text of the books with the shelf query language: words, \"phrases\", prefix*, -excluded, and author: title: tag: lang: year: format: status: filters. Returns works, best first, with places in the text (location) that read_passage accepts.")]
    public SearchBooksResult SearchBooks(
        [Description("The query, e.g. 'event sourcing tag:architektur year:>=2015'")] string query,
        [Description("Largest number of works, 1 to 100 (default 20)")] int limit = LibrarySearch.DefaultLimit)
    {
        if (limit is < 1 or > MaxLimit)
        {
            throw new McpException($"limit must be between 1 and {MaxLimit}.");
        }

        SearchQuery parsed;
        try
        {
            parsed = QueryParser.Parse(query);
        }
        catch (QueryException e)
        {
            throw new McpException(e.Message);
        }

        if (parsed.IsEmpty)
        {
            throw new McpException("the query has nothing to search for");
        }

        using var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(root));
        var hits = LibrarySearch.Search(connection, parsed, limit);
        return new SearchBooksResult(
            query,
            hits.Count,
            [.. hits.Select(h => new BookSummary(
                h.WorkId.ToString(),
                h.Title,
                [.. h.Authors],
                h.Year,
                h.Language,
                [.. h.Formats],
                Math.Round(h.Score, 2),
                [.. h.Hits.Select(c => new PassageRef(c.File, c.Format, c.Locator, c.Where, c.Snippet))]))]);
    }

    [Description("Metadata, files, tags and table of contents of a work. Locations in the table of contents can be given to read_passage.")]
    public BookDetails GetBook([Description("The ID of a work, or the start of it (at least one character that names exactly one work)")] string id) =>
        Details(Find(id), withToc: true);

    [Description("Plain text of one place in a work, as found by search_books or listed in the table of contents. Long places are read in parts with offset.")]
    public PassageText ReadPassage(
        [Description("The ID of a work, or the start of it")] string id,
        [Description("The location: 'page=12', an EPUB location from search_books or get_book, or just a page number")] string location,
        [Description("Largest number of characters to return (default 4000; the hard maximum is set with mcp.max_chars, 20000 by default)")] int? maxChars = null,
        [Description("Characters to skip at the start of the place, to read on after a part that was cut (default 0)")] int offset = 0,
        [Description("The file name, if the work has several files with the same location (default: the first with text there)")] string? file = null)
    {
        var hardMax = settings.Mcp.MaxChars;
        var wanted = maxChars ?? Math.Min(DefaultMaxChars, hardMax);
        if (wanted < 1 || wanted > hardMax)
        {
            throw new McpException($"maxChars must be between 1 and {hardMax.ToString(CultureInfo.InvariantCulture)}.");
        }

        if (offset < 0)
        {
            throw new McpException("offset must not be negative.");
        }

        var work = Find(id);
        var place = long.TryParse(location, NumberStyles.None, CultureInfo.InvariantCulture, out var page) && page >= 1
            ? Locators.PdfPage((int)page)
            : location.Trim();

        using var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(root));
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT f.id, f.rel_path, c.text
            FROM chunks c JOIN files f ON f.id = c.file_id
            WHERE f.work_id = $work AND c.locator = $locator
            ORDER BY f.id, c.ordinal
            """;
        command.Parameters.AddWithValue("$work", work.Id.ToString());
        command.Parameters.AddWithValue("$locator", place);
        var parts = new List<(long File, string Path, string Text)>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                parts.Add((reader.GetInt64(0), reader.GetString(1), reader.GetString(2)));
            }
        }

        if (file is not null)
        {
            parts = [.. parts.Where(p => string.Equals(System.IO.Path.GetFileName(p.Path), file, StringComparison.Ordinal))];
        }

        if (parts.Count == 0)
        {
            throw new McpException($"no text at '{location}' in '{work.Title}'; use a location from search_books or get_book");
        }

        var first = parts[0].File;
        var path = parts[0].Path;
        var text = string.Join(' ', parts.Where(p => p.File == first).Select(p => p.Text));
        if (offset > 0 && offset >= text.Length)
        {
            throw new McpException($"offset {offset.ToString(CultureInfo.InvariantCulture)} is beyond the end of the text ({text.Length.ToString(CultureInfo.InvariantCulture)} characters).");
        }

        var length = Math.Min(wanted, text.Length - offset);
        return new PassageText(
            work.Id.ToString(),
            path,
            place,
            Locators.Describe(place),
            text.Substring(offset, length),
            offset,
            text.Length,
            offset + length < text.Length);
    }

    [Description("The tags of the library as a tree, with the number of works under each.")]
    public TagTree ListTags()
    {
        using var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(root));
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT t.path, wt.work_id FROM tags t JOIN work_tags wt ON wt.tag_id = t.id ORDER BY t.path";
        var direct = new SortedDictionary<string, HashSet<string>>(StringComparer.Ordinal);
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                if (!direct.TryGetValue(reader.GetString(0), out var set))
                {
                    direct[reader.GetString(0)] = set = new HashSet<string>(StringComparer.Ordinal);
                }

                set.Add(reader.GetString(1));
            }
        }

        return new TagTree(direct.Count, [.. Build(direct, string.Empty)]);
    }

    [Description("Duplicate files in the library: identical files (same content) and different files of one work in the same format. Read-only; nothing is deleted.")]
    public DuplicatesResult FindDuplicates()
    {
        var groups = LibraryDuplicates.Find(root);
        return new DuplicatesResult(
            groups.Count,
            [.. groups.Select(g => new DuplicateSet(
                g.Kind == DuplicateKind.Identical ? "identical" : "same-format",
                g.Format,
                [.. g.Files.Select(f => new DuplicateEntry(f.Work.ToString(), f.Title, f.Path, f.Size, f.Pages))],
                g.Keep))]);
    }

    [Description("A dry run of 'shelf organize': which files would move to which folders and names. Nothing is moved.")]
    public OrganizationProposal ProposeOrganization(
        [Description("A path pattern such as '{author_sort}/{title} ({year})/{title}.{ext}' (default: the pattern of the library)")] string? pattern = null)
    {
        PathPattern chosen;
        try
        {
            chosen = pattern is null ? settings.Organize.Pattern : PathPattern.Parse(pattern);
        }
        catch (PatternException e)
        {
            throw new McpException($"pattern: {e.Message}");
        }

        OrganizeResult result;
        try
        {
            result = LibraryOrganizer.Organize(root, chosen, apply: false);
        }
        catch (Exception e) when (e is IndexException or LibraryEditException)
        {
            throw new McpException(e.Message);
        }

        return new OrganizationProposal(
            chosen.Text,
            result.Files,
            result.Works,
            result.Unchanged,
            result.Changes.Count > MaxMoves,
            [.. result.Changes.Take(MaxMoves).Select(c => new PlannedMove(c.Work.ToString(), c.From, c.To))],
            [.. result.Collisions.Take(MaxMoves).Select(c => new SkippedMove(c.Work.ToString(), c.From, c.To, c.Reason))]);
    }

    // ---- resources ----

    /// <summary>Metadata of a work (without the table of contents: see <see cref="TocJson"/>).</summary>
    public string BookJson(string id)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Serialize(Details(Find(id), withToc: false), McpJsonContext.Default.BookDetails);
        }
        catch (McpException e)
        {
            throw new McpProtocolException(e.Message, McpErrorCode.InvalidParams);
        }
    }

    public string TocJson(string id)
    {
        try
        {
            return System.Text.Json.JsonSerializer.Serialize(Details(Find(id), withToc: true).Toc, McpJsonContext.Default.TocItemArray);
        }
        catch (McpException e)
        {
            throw new McpProtocolException(e.Message, McpErrorCode.InvalidParams);
        }
    }

    // ---- helpers ----

    private LibraryWork Find(string id)
    {
        try
        {
            return WorkLookup.Resolve(root, id, root);
        }
        catch (Exception e) when (e is LibraryEditException or IndexException)
        {
            throw new McpException(e.Message);
        }
    }

    private BookDetails Details(LibraryWork work, bool withToc)
    {
        var sidecar = LibraryEditor.Read(root, work);
        using var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(root));
        var pages = new Dictionary<string, int?>(StringComparer.Ordinal);
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT rel_path, page_count FROM files WHERE work_id = $id";
            command.Parameters.AddWithValue("$id", work.Id.ToString());
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                pages[System.IO.Path.GetFileName(reader.GetString(0))] = reader.IsDBNull(1) ? null : reader.GetInt32(1);
            }
        }

        var files = new List<BookFile>();
        var toc = new List<TocItem>();
        foreach (var entry in sidecar.Files)
        {
            var full = LibraryEditor.FilePath(root, work, entry.Name);
            var info = new FileInfo(full);
            files.Add(new BookFile(entry.Name, System.IO.Path.GetExtension(entry.Name).TrimStart('.').ToLowerInvariant(), info.Exists ? info.Length : 0, pages.GetValueOrDefault(entry.Name)));
            if (withToc && toc.Count == 0 && info.Exists)
            {
                toc.AddRange(TocExtractor.Read(full).Select(t => new TocItem(t.Title, t.Level, t.Locator)));
            }
        }

        return new BookDetails(
            sidecar.Id.ToString(),
            sidecar.Title,
            sidecar.Subtitle,
            [.. sidecar.Authors],
            sidecar.Language,
            sidecar.Year,
            sidecar.Isbn13,
            sidecar.Publisher,
            sidecar.Series,
            sidecar.SeriesIndex,
            sidecar.Description,
            [.. sidecar.Tags],
            sidecar.ReadStatus.ToString().ToLowerInvariant(),
            [.. files],
            [.. toc]);
    }

    private static List<TagNode> Build(SortedDictionary<string, HashSet<string>> direct, string prefix)
    {
        var names = direct.Keys
            .Where(k => prefix.Length == 0 || k.StartsWith(prefix + "/", StringComparison.Ordinal))
            .Select(k => (prefix.Length == 0 ? k : k[(prefix.Length + 1)..]).Split('/')[0])
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);
        var nodes = new List<TagNode>();
        foreach (var name in names)
        {
            var path = prefix.Length == 0 ? name : prefix + "/" + name;
            var below = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (tag, works) in direct)
            {
                if (tag == path || tag.StartsWith(path + "/", StringComparison.Ordinal))
                {
                    below.UnionWith(works);
                }
            }

            nodes.Add(new TagNode(name, path, below.Count, direct.TryGetValue(path, out var own) ? own.Count : 0, [.. Build(direct, path)]));
        }

        return nodes;
    }
}
