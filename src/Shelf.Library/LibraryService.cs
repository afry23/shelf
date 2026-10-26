using System.Globalization;
using System.Text.Json.Nodes;
using System.Text;
using Microsoft.Data.Sqlite;
using Shelf.Core.Configuration;
using Shelf.Core.Model;
using Shelf.Core.Organization;
using Shelf.Core.Search;
using Shelf.Extraction;
using Shelf.Index;
using Shelf.Organize;
using Shelf.Semantic;

namespace Shelf.Library;

/// <summary>
/// The operations of the library for every access path (MCP, web): search, details, passages, tags, duplicates and
/// organization proposals over one library. Errors the caller can act on (unknown work, bad query, no text at a location)
/// are <see cref="LibraryException"/>s. Only the tag methods change anything.
/// </summary>
public sealed partial class LibraryService(string root, ShelfSettings settings, SemanticBackend semantic, IReadOnlyList<IMetadataSource>? metadataSources = null)
{
    public const int DefaultMaxChars = 4_000;
    public const int MaxLimit = 100;
    public const int MaxMoves = 200;

    // The SDK handles requests at the same time; two changes of one sidecar must not overlap (each reads, changes and replaces it).
    private readonly Lock _writeGate = new();

    // ---- tools ----

    public SearchBooksResult SearchBooks(
        string query,
        int limit = LibrarySearch.DefaultLimit)
    {
        if (limit is < 1 or > MaxLimit)
        {
            throw new LibraryException($"limit must be between 1 and {MaxLimit}.");
        }

        SearchQuery parsed;
        try
        {
            parsed = QueryParser.Parse(query);
        }
        catch (QueryException e)
        {
            throw new LibraryException(e.Message);
        }

        if (parsed.IsEmpty)
        {
            throw new LibraryException("the query has nothing to search for");
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

    public BookDetails GetBook(string id) =>
        Details(Find(id), withToc: true);

    public PassageText ReadPassage(
        string id,
        string location,
        int? maxChars = null,
        int offset = 0,
        string? file = null)
    {
        var hardMax = settings.Mcp.MaxChars;
        var wanted = maxChars ?? Math.Min(DefaultMaxChars, hardMax);
        if (wanted < 1 || wanted > hardMax)
        {
            throw new LibraryException($"maxChars must be between 1 and {hardMax.ToString(CultureInfo.InvariantCulture)}.");
        }

        if (offset < 0)
        {
            throw new LibraryException("offset must not be negative.");
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
            throw new LibraryException($"no text at '{location}' in '{work.Title}'; use a location from search_books or get_book", LibraryErrorKind.NotFound);
        }

        var first = parts[0].File;
        var path = parts[0].Path;
        var text = string.Join(' ', parts.Where(p => p.File == first).Select(p => p.Text));
        if (offset > 0 && offset >= text.Length)
        {
            throw new LibraryException($"offset {offset.ToString(CultureInfo.InvariantCulture)} is beyond the end of the text ({text.Length.ToString(CultureInfo.InvariantCulture)} characters).");
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

    public SemanticSearchResult SemanticSearch(
        string query,
        string level = "work",
        bool hybrid = false,
        int limit = 10)
    {
        if (limit is < 1 or > MaxLimit)
        {
            throw new LibraryException($"limit must be between 1 and {MaxLimit}.");
        }

        if (level is not ("work" or "chunk"))
        {
            throw new LibraryException("level must be 'work' or 'chunk'.");
        }

        var text = query.Trim();
        if (text.Length == 0)
        {
            throw new LibraryException("the query has nothing to search for");
        }

        SearchQuery? words = null;
        if (hybrid)
        {
            try
            {
                words = QueryParser.Parse(text);
            }
            catch (QueryException e)
            {
                throw new LibraryException(e.Message);
            }

            if (words.Filters.Count > 0 || words.Exclude.Count > 0)
            {
                throw new LibraryException("hybrid takes words and phrases only; use search_books for filters and exclusions");
            }

            if (words.Include.Count == 0)
            {
                throw new LibraryException("the query has nothing to search for");
            }
        }

        using var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(root));
        VectorStore? store;
        try
        {
            store = VectorStore.OpenExisting(connection);
        }
        catch (VectorStoreException e)
        {
            throw new LibraryException(e.Message);
        }

        var vector = store is null ? null : EmbedQuery(store, text);
        var passages = level == "chunk";
        List<SemanticHit> hits;
        if (hybrid)
        {
            hits = passages
                ? HybridSearch.SearchPassages(connection, store, vector, words!, limit)
                : HybridSearch.SearchWorks(connection, store, vector, words!, limit);
        }
        else if (store is null)
        {
            hits = [];
        }
        else
        {
            hits = passages
                ? Shelf.Semantic.SemanticSearch.SearchPassages(connection, store, vector!, limit)
                : Shelf.Semantic.SemanticSearch.SearchWorks(connection, store, vector!, limit);
        }

        var coverage = Shelf.Semantic.SemanticSearch.Coverage(connection, store);
        var empty = store is null || (store.Count(VectorKind.Work) == 0 && store.Count(VectorKind.Chunk) == 0);
        return new SemanticSearchResult(
            text,
            level,
            hybrid ? "hybrid" : "semantic",
            hits.Count,
            coverage.PendingFiles,
            coverage.WorksWithoutVector,
            coverage.Note(empty),
            [.. hits.Select(h => new SemanticItem(
                h.Work.Id.ToString(),
                h.Work.Title,
                [.. h.Work.Authors],
                h.Work.Year,
                h.Work.Language,
                [.. h.Work.Formats],
                Math.Round(h.Score, 4),
                h.Passage is { } p ? new PassageRef(p.File, p.Format, p.Locator, p.Where, p.Snippet) : null))]);
    }

    public SimilarBooksResult SimilarBooks(
        string id,
        int limit = 10)
    {
        if (limit is < 1 or > MaxLimit)
        {
            throw new LibraryException($"limit must be between 1 and {MaxLimit}.");
        }

        var work = Find(id);
        using var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(root));
        VectorStore? store;
        try
        {
            store = VectorStore.OpenExisting(connection);
        }
        catch (VectorStoreException e)
        {
            throw new LibraryException(e.Message);
        }

        var hits = store is null ? null : Shelf.Semantic.SemanticSearch.SimilarWorks(connection, store, work.Id.ToString(), limit);
        var coverage = Shelf.Semantic.SemanticSearch.Coverage(connection, store);
        return new SimilarBooksResult(
            work.Id.ToString(),
            work.Title,
            hits?.Count ?? 0,
            Shelf.Semantic.SemanticSearch.SimilarNote(hits, store, coverage),
            [.. (hits ?? []).Select(h => new SemanticItem(
                h.Work.Id.ToString(),
                h.Work.Title,
                [.. h.Work.Authors],
                h.Work.Year,
                h.Work.Language,
                [.. h.Work.Formats],
                Math.Round(h.Score, 4),
                null))]);
    }

    public TagChange AddTags(
        string id,
        string[] tags) =>
        ChangeTags(id, tags, LibraryTags.Add);

    public TagChange RemoveTags(
        string id,
        string[] tags) =>
        ChangeTags(id, tags, LibraryTags.Remove);

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

    /// <summary>Renames a tag and the tags below it on every work (F-TAG-4); the new name must be unused. One undoable step.</summary>
    /// <exception cref="LibraryException">Invalid: a bad name. NotFound: no work has the tag. Conflict: the new name is in use.</exception>
    public TagMoveResult RenameTag(string from, string to) => MoveTag(from, to, merge: false);

    /// <summary>Puts a tag and the tags below it into another tag, which may exist (F-TAG-4). One undoable step.</summary>
    /// <exception cref="LibraryException">Invalid: a bad name. NotFound: no work has the tag.</exception>
    public TagMoveResult MergeTag(string from, string into) => MoveTag(from, into, merge: true);

    private TagMoveResult MoveTag(string from, string to, bool merge)
    {
        if (!TagNames.TryNormalize(from, out var source, out var problem))
        {
            throw new LibraryException($"from: {problem}");
        }

        if (!TagNames.TryNormalize(to, out var target, out problem))
        {
            throw new LibraryException($"{(merge ? "into" : "to")}: {problem}");
        }

        if (source == target)
        {
            throw new LibraryException("the two names are the same tag");
        }

        if (merge && (TagNames.IsSameOrBelow(target, source) || TagNames.IsSameOrBelow(source, target)))
        {
            throw new LibraryException("a tag cannot be merged into a tag above or below it");
        }

        lock (_writeGate)
        {
            try
            {
                var existing = LibraryTags.List(root).Select(t => t.Tag).ToList();
                if (!existing.Any(t => TagNames.IsSameOrBelow(t, source)))
                {
                    throw new LibraryException($"no work has the tag '{source}'", LibraryErrorKind.NotFound);
                }

                if (!merge && existing.Any(t => TagNames.IsSameOrBelow(t, target)))
                {
                    throw new LibraryException($"the tag '{target}' already exists; merge into it instead", LibraryErrorKind.Conflict);
                }

                var result = merge ? LibraryTags.Merge(root, source, target) : LibraryTags.Rename(root, source, target);
                return new TagMoveResult(source, target, result.Changed);
            }
            catch (Exception e) when (e is LibraryEditException or SidecarException or JournalException or IndexException)
            {
                throw new LibraryException(e.Message);
            }
        }
    }

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

    public OrganizationProposal ProposeOrganization(
        string? pattern = null)
    {
        PathPattern chosen;
        try
        {
            chosen = pattern is null ? settings.Organize.Pattern : PathPattern.Parse(pattern);
        }
        catch (PatternException e)
        {
            throw new LibraryException($"pattern: {e.Message}");
        }

        OrganizeResult result;
        try
        {
            result = LibraryOrganizer.Organize(root, chosen, apply: false);
        }
        catch (Exception e) when (e is IndexException or LibraryEditException)
        {
            throw new LibraryException(e.Message);
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

    // ---- the library as a list, metadata, tag sets (the web API) ----

    public const string SortNames = "title, author, year, added, updated";

    /// <summary>The works that pass the filters in <paramref name="query"/>, one page, with the counts per facet.</summary>
    /// <param name="query">Filters of the query language only (<c>tag:cloud -lang:de</c>); words are refused.</param>
    public BrowsePage Browse(string? query, string sort = "title", string order = "asc", int offset = 0, int limit = BrowseOptions.DefaultLimit)
    {
        if (limit is < 1 or > BrowseOptions.MaxLimit)
        {
            throw new LibraryException($"limit must be between 1 and {BrowseOptions.MaxLimit}.");
        }

        if (offset < 0)
        {
            throw new LibraryException("offset must not be negative.");
        }

        if (!Enum.TryParse<WorkSort>(sort, ignoreCase: true, out var chosen) || !Enum.IsDefined(chosen))
        {
            throw new LibraryException($"sort must be one of {SortNames}.");
        }

        if (order is not ("asc" or "desc"))
        {
            throw new LibraryException("order must be asc or desc.");
        }

        SearchQuery parsed;
        try
        {
            parsed = QueryParser.Parse(query ?? string.Empty);
        }
        catch (QueryException e)
        {
            throw new LibraryException(e.Message);
        }

        if (parsed.Include.Count > 0)
        {
            throw new LibraryException("the list takes filters only (tag: author: title: lang: year: format: status:); use search for words");
        }

        try
        {
            using var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(root));
            return LibraryBrowse.Browse(connection, parsed, new BrowseOptions(chosen, order == "desc", offset, limit));
        }
        catch (IndexException e)
        {
            throw new LibraryException(e.Message);
        }
    }

    /// <summary>
    /// Changes metadata as a JSON merge patch: a field that is absent stays, a field set to <c>null</c> is removed. The
    /// rules are those of <c>shelf edit</c>. Nothing is changed unless every field is valid.
    /// </summary>
    public BookDetails EditMetadata(string id, JsonObject patch)
    {
        ArgumentNullException.ThrowIfNull(patch);
        if (patch.Count == 0)
        {
            throw new LibraryException("nothing to change; give at least one field such as title");
        }

        var work = Find(id);
        var edits = new List<Func<WorkSidecar, WorkSidecar>>();
        foreach (var (name, value) in patch)
        {
            edits.Add(MetadataPatch.Edit(name, value));
        }

        lock (_writeGate)
        {
            try
            {
                LibraryEditor.Update(root, work, sidecar => edits.Aggregate(sidecar, (current, edit) => edit(current)));
            }
            catch (Exception e) when (e is LibraryEditException or SidecarException or IndexException)
            {
                throw new LibraryException(e.Message);
            }
        }

        return Details(work, withToc: false);
    }

    /// <summary>The cite key that the metadata of a work suggest, different from the keys of the other works. Nothing is written.</summary>
    public CiteKeySuggestion SuggestCiteKey(string id)
    {
        var work = Find(id);
        try
        {
            return new CiteKeySuggestion(work.Id.ToString(), LibraryCiteKeys.Suggest(root, work));
        }
        catch (Exception e) when (e is SidecarException or IndexException)
        {
            throw new LibraryException(e.Message);
        }
    }

    /// <summary>Gives a work exactly these tags, in one undoable step. Every name is checked first; one that is refused changes nothing.</summary>
    public TagSetResult SetTags(string id, string[] tags)
    {
        ArgumentNullException.ThrowIfNull(tags);
        var names = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var tag in tags)
        {
            if (!TagNames.TryNormalize(tag, out var name, out var error))
            {
                throw new LibraryException($"tag '{tag}': {error}");
            }

            names.Add(name);
        }

        var work = Find(id);
        lock (_writeGate)
        {
            try
            {
                var before = LibraryEditor.Read(root, work).Tags;
                var added = names.Count(n => !before.Contains(n));
                var removed = before.Count(t => !names.Contains(t));
                if (added + removed > 0)
                {
                    LibraryTags.SetTags(root, work.Id, [.. names]);
                }

                return new TagSetResult(work.Id.ToString(), [.. names], added, removed);
            }
            catch (Exception e) when (e is LibraryEditException or SidecarException or JournalException or IndexException)
            {
                throw new LibraryException(e.Message);
            }
        }
    }

    // ---- files and duplicates (the web API) ----

    /// <summary>
    /// The file with this ID (see <see cref="FileIds"/>). The name must be an entry of the sidecar of the work, so a name
    /// such as <c>../x</c> finds nothing; the file must lie inside the library and exist.
    /// </summary>
    /// <exception cref="LibraryException">With the kind <see cref="LibraryErrorKind.NotFound"/>: no such work, file or folder entry.</exception>
    public FileLocation ResolveFile(string fileId)
    {
        if (!FileIds.TryParse(fileId, out var workId, out var name))
        {
            throw new LibraryException($"'{fileId}' is not the ID of a file (the ID of a work, a tilde and the file name)", LibraryErrorKind.NotFound);
        }

        if (!WorkLookup.LooksLikeId(workId))
        {
            throw new LibraryException($"no work matches '{workId}'", LibraryErrorKind.NotFound);
        }

        var work = Find(workId);
        var sidecar = ReadSidecar(work);
        if (!sidecar.Files.Any(f => string.Equals(f.Name, name, StringComparison.Ordinal)))
        {
            throw new LibraryException($"no file '{name}' in '{work.Title}'", LibraryErrorKind.NotFound);
        }

        return Locate(work, name)
            ?? throw new LibraryException($"the file '{name}' of '{work.Title}' is missing from the library folder", LibraryErrorKind.NotFound);
    }

    /// <summary>
    /// The file that <c>Read</c> opens (F-RDR-5): the first of the configured format (<c>reader.preferred_format</c>, EPUB by default),
    /// else the first file that has a reader, else the first file. Null for a work without files.
    /// </summary>
    public string? PreferredFile(IReadOnlyList<BookFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var wanted = settings.Reader.PreferredFormat;
        return (files.FirstOrDefault(f => f.Format == wanted)
            ?? files.FirstOrDefault(f => f.Format is "epub" or "pdf")
            ?? files.FirstOrDefault())?.Name;
    }

    /// <summary>
    /// Remembers where a person stopped reading a file (F-RDR-4) in <c>book.json</c>. The location must fit the format: <c>page=N</c>
    /// for a PDF, <c>epubcfi(...)</c> for an EPUB. The same location as before changes nothing.
    /// </summary>
    /// <exception cref="LibraryException">Invalid: the location is not one of this kind of file. NotFound: no such file.</exception>
    public ReadingPosition SaveProgress(string fileId, string locator)
    {
        var file = ResolveFile(fileId);
        if (LocatorProblem(file.Format, locator) is { } problem)
        {
            throw new LibraryException(problem);
        }

        var work = Find(file.Work.ToString());
        lock (_writeGate)
        {
            try
            {
                var current = LibraryEditor.Read(root, work).Files.First(f => f.Name == file.Name).Progress;
                if (current is not null && current.Locator == locator)
                {
                    return Position(current);
                }

                var saved = new FileProgress(locator, DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
                LibraryEditor.SetProgress(root, work, file.Name, saved);
                return Position(saved);
            }
            catch (Exception e) when (e is SidecarException or IndexException)
            {
                throw new LibraryException(e.Message);
            }
        }
    }

    /// <summary>Why this location is none of a file of this format, or null.</summary>
    private static string? LocatorProblem(string format, string locator) => format switch
    {
        "pdf" => PageLocator().IsMatch(locator)
            ? null
            : "locator of a PDF must be page=N, with N a page number from 1",
        "epub" => locator.Length <= 2_000 && locator.StartsWith("epubcfi(", StringComparison.Ordinal) && locator.EndsWith(')')
            ? null
            : "locator of an EPUB must be an epubcfi(...)",
        _ => $"files of the format {format} have no reading position",
    };

    // ---- annotations (F-ANN-1 to F-ANN-3) ----

    private const int MaxQuoteLength = 20_000;
    private const int MaxNoteLength = 200_000;

    /// <summary>The highlights and notes of one file, oldest first.</summary>
    /// <exception cref="LibraryException">NotFound: no such file.</exception>
    public AnnotationView[] ListAnnotations(string fileId)
    {
        var file = ResolveFile(fileId);
        var work = Find(file.Work.ToString());
        return [.. ReadAnnotations(work).Where(a => a.File == file.Name).Select(a => View(work, a))];
    }

    /// <summary>The highlights and notes of all files of a work, oldest first.</summary>
    /// <exception cref="LibraryException">NotFound: no such work.</exception>
    public AnnotationView[] ListWorkAnnotations(string workId)
    {
        var work = Find(workId);
        return [.. ReadAnnotations(work).Select(a => View(work, a))];
    }

    /// <summary>The highlights and notes of a work, with the work's title.</summary>
    /// <exception cref="LibraryException">NotFound: no such work.</exception>
    public AnnotationList GetAnnotations(string workId)
    {
        var work = Find(workId);
        return new AnnotationList(work.Id.ToString(), work.Title, ListWorkAnnotations(workId));
    }

    /// <summary>The metadata, highlights and notes of a work as one Markdown file, named after the cite key (F-ANN-7).</summary>
    /// <exception cref="LibraryException">Invalid: the work has no cite key. NotFound: no such work.</exception>
    public MarkdownExport ExportMarkdown(string workId)
    {
        var work = Find(workId);
        var sidecar = ReadSidecar(work);
        try
        {
            return new MarkdownExport(WorkExport.FileName(sidecar), WorkExport.Render(sidecar, ReadAnnotations(work)));
        }
        catch (InvalidOperationException e)
        {
            throw new LibraryException(e.Message);
        }
    }

    /// <summary>Adds a note without a place in the text to a file of the work: the named file, else the only file of the work.</summary>
    /// <exception cref="LibraryException">Invalid: the text is empty, or the work has several files and none is named. NotFound: no such work or file.</exception>
    public AnnotationView AddNote(string workId, string text, string? file = null)
    {
        var work = Find(workId);
        var files = ReadSidecar(work).Files;
        if (file is null)
        {
            if (files.Count != 1)
            {
                throw new LibraryException($"'{work.Title}' has {files.Count} files; name one in file ({string.Join(", ", files.Select(f => f.Name))})");
            }

            file = files[0].Name;
        }

        return AddAnnotation(FileIds.Make(work.Id.ToString(), file), new AnnotationDraft("note", null, null, null, null, null, text));
    }

    /// <summary>Adds a highlight or a note to a file. A highlight needs a location that fits the format and the marked text.</summary>
    /// <exception cref="LibraryException">Invalid: the draft is none of a highlight or a note. NotFound: no such file.</exception>
    public AnnotationView AddAnnotation(string fileId, AnnotationDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var file = ResolveFile(fileId);
        if (!AnnotationsFormat.TryParseKind(draft.Kind, out var kind))
        {
            throw new LibraryException("kind is required and must be highlight or note");
        }

        var note = string.IsNullOrWhiteSpace(draft.Note) ? null : draft.Note;
        if (note is { Length: > MaxNoteLength })
        {
            throw new LibraryException($"note must have at most {MaxNoteLength} characters");
        }

        Annotation created;
        var now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        if (kind == AnnotationKind.Note)
        {
            if (draft.Locator is not null || draft.QuoteExact is not null || draft.Color is not null || draft.Rects is not null)
            {
                throw new LibraryException("a note has no locator, quote, color or rects; leave them out or make a highlight");
            }

            if (note is null)
            {
                throw new LibraryException("note is required: the text of the note");
            }

            created = new Annotation(Ulid.New(), file.Name, kind, null, null, null, note, now, now);
        }
        else
        {
            if (string.IsNullOrEmpty(draft.Locator))
            {
                throw new LibraryException("locator is required for a highlight: the place in the file");
            }

            if (LocatorProblem(file.Format, draft.Locator) is { } problem)
            {
                throw new LibraryException(problem);
            }

            if (string.IsNullOrWhiteSpace(draft.QuoteExact) || draft.QuoteExact.Length > MaxQuoteLength)
            {
                throw new LibraryException($"quote.exact is required for a highlight: the marked text, at most {MaxQuoteLength} characters");
            }

            var color = draft.Color ?? AnnotationsSidecar.DefaultColor;
            if (!AnnotationsSidecar.Colors.Contains(color))
            {
                throw new LibraryException($"color must be one of {string.Join(", ", AnnotationsSidecar.Colors)}");
            }

            created = new Annotation(
                Ulid.New(), file.Name, kind, draft.Locator, new AnnotationQuote(draft.QuoteExact, draft.QuotePrefix, draft.QuoteSuffix), color, note, now, now,
                Rects(draft.Rects));
        }

        var work = Find(file.Work.ToString());
        lock (_writeGate)
        {
            try
            {
                LibraryAnnotations.Add(root, work, created);
            }
            catch (Exception e) when (e is SidecarException or IndexException)
            {
                throw new LibraryException(e.Message);
            }
        }

        return View(work, created);
    }

    /// <summary>Changes the note or the color of an annotation.</summary>
    /// <exception cref="LibraryException">Invalid: the color or the note is none. NotFound: no such file or annotation.</exception>
    public AnnotationView UpdateAnnotation(string fileId, string annotationId, AnnotationChanges changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        var file = ResolveFile(fileId);
        var work = Find(file.Work.ToString());
        var id = AnnotationId(annotationId);
        string? note = null;
        if (changes.NoteGiven)
        {
            note = string.IsNullOrWhiteSpace(changes.Note) ? null : changes.Note;
            if (note is { Length: > MaxNoteLength })
            {
                throw new LibraryException($"note must have at most {MaxNoteLength} characters");
            }
        }

        if (changes.ColorGiven && (changes.Color is null || !AnnotationsSidecar.Colors.Contains(changes.Color)))
        {
            throw new LibraryException($"color must be one of {string.Join(", ", AnnotationsSidecar.Colors)}");
        }

        Annotation? updated = null;
        lock (_writeGate)
        {
            try
            {
                var existing = LibraryAnnotations.List(root, work).FirstOrDefault(a => a.Id == id && a.File == file.Name)
                    ?? throw new LibraryException($"no annotation '{annotationId}' in '{file.Name}'", LibraryErrorKind.NotFound);
                if (existing.Kind == AnnotationKind.Note && changes.NoteGiven && note is null)
                {
                    throw new LibraryException("note is required: a note without text is deleted instead");
                }

                if (existing.Kind == AnnotationKind.Note && changes.ColorGiven)
                {
                    throw new LibraryException("a note has no color");
                }

                var now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                updated = existing with
                {
                    Note = changes.NoteGiven ? note : existing.Note,
                    Color = changes.ColorGiven ? changes.Color : existing.Color,
                    UpdatedAt = now,
                };
                LibraryAnnotations.Update(root, work, id, _ => updated);
            }
            catch (Exception e) when (e is SidecarException or IndexException)
            {
                throw new LibraryException(e.Message);
            }
        }

        return View(work, updated);
    }

    /// <summary>Deletes an annotation.</summary>
    /// <exception cref="LibraryException">NotFound: no such file or annotation.</exception>
    public void DeleteAnnotation(string fileId, string annotationId)
    {
        var file = ResolveFile(fileId);
        var work = Find(file.Work.ToString());
        var id = AnnotationId(annotationId);
        lock (_writeGate)
        {
            try
            {
                if (!LibraryAnnotations.List(root, work).Any(a => a.Id == id && a.File == file.Name)
                    || !LibraryAnnotations.Delete(root, work, id))
                {
                    throw new LibraryException($"no annotation '{annotationId}' in '{file.Name}'", LibraryErrorKind.NotFound);
                }
            }
            catch (Exception e) when (e is SidecarException or IndexException)
            {
                throw new LibraryException(e.Message);
            }
        }
    }

    /// <summary>The areas of a highlight, or none; the format checks that they lie inside the page.</summary>
    private static List<AnnotationRect>? Rects(double[][]? given)
    {
        if (given is null)
        {
            return null;
        }

        if (given.Length is < 1 or > AnnotationsFormat.MaxRects || given.Any(r => r is null || r.Length != 4 || r.Any(n => n is < 0 or > 1)))
        {
            throw new LibraryException($"rects must be 1 to {AnnotationsFormat.MaxRects} areas of four fractions each (x, y, width, height), from 0 to 1");
        }

        var rects = given.Select(r => new AnnotationRect(r[0], r[1], r[2], r[3])).ToList();
        if (rects.Any(r => r.X + r.Width > 1.0001 || r.Y + r.Height > 1.0001))
        {
            throw new LibraryException("rects must lie inside the page: x + width and y + height can be at most 1");
        }

        return rects;
    }

    private static Ulid AnnotationId(string text) =>
        Ulid.TryParse(text, out var id) ? id : throw new LibraryException($"no annotation '{text}'", LibraryErrorKind.NotFound);

    private IReadOnlyList<Annotation> ReadAnnotations(LibraryWork work)
    {
        try
        {
            return LibraryAnnotations.List(root, work);
        }
        catch (SidecarException e)
        {
            throw new LibraryException(e.Message);
        }
    }

    private static AnnotationView View(LibraryWork work, Annotation a) =>
        new(
            a.Id.ToString(),
            FileIds.Make(work.Id.ToString(), a.File),
            AnnotationsFormat.KindName(a.Kind),
            a.Locator,
            a.Quote is null ? null : new AnnotationQuoteView(a.Quote.Exact, a.Quote.Prefix, a.Quote.Suffix),
            a.Color,
            a.Note,
            Time(a.CreatedAt),
            Time(a.UpdatedAt),
            a.Rects is null ? null : [.. a.Rects.Select(r => new[] { r.X, r.Y, r.Width, r.Height })]);

    private static string Time(DateTimeOffset time) =>
        time.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture);

    private static ReadingPosition Position(FileProgress progress) =>
        new(progress.Locator, progress.UpdatedAt.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture));

    [System.Text.RegularExpressions.GeneratedRegex("^page=[1-9][0-9]{0,8}$")]
    private static partial System.Text.RegularExpressions.Regex PageLocator();

    /// <summary>The cover image of a work (F-IMP-5).</summary>
    /// <exception cref="LibraryException">With the kind <see cref="LibraryErrorKind.NotFound"/>: no such work, no cover, or the file is gone.</exception>
    public FileLocation ResolveCover(string id)
    {
        var work = Find(id);
        var name = ReadSidecar(work).Cover
            ?? throw new LibraryException($"'{work.Title}' has no cover", LibraryErrorKind.NotFound);
        return Locate(work, name)
            ?? throw new LibraryException($"the cover '{name}' of '{work.Title}' is missing from the library folder", LibraryErrorKind.NotFound);
    }

    /// <summary>True when the work has a cover and its file is there.</summary>
    public bool HasCover(string id)
    {
        var work = Find(id);
        return ReadSidecar(work).Cover is { } name && Locate(work, name) is not null;
    }

    private WorkSidecar ReadSidecar(LibraryWork work)
    {
        try
        {
            return LibraryEditor.Read(root, work);
        }
        catch (SidecarException e)
        {
            throw new LibraryException(e.Message);
        }
    }

    /// <summary>The file with this name in the folder of the work, if it is a plain name, lies inside the library and exists.</summary>
    private FileLocation? Locate(LibraryWork work, string name)
    {
        if (name.Length == 0 || name != Path.GetFileName(name))
        {
            return null;
        }

        var full = Path.GetFullPath(LibraryEditor.FilePath(root, work, name));
        var inside = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        var info = new FileInfo(full);
        if (!full.StartsWith(inside, StringComparison.Ordinal) || !info.Exists)
        {
            return null;
        }

        return new FileLocation(work.Id, work.Title, name, Path.GetExtension(name).TrimStart('.').ToLowerInvariant(), full, info.Length, info.LastWriteTimeUtc);
    }

    /// <summary>The groups of duplicate files (F-DUP-1, F-DUP-2), as the index finds them; pairs marked as not duplicates are left out.</summary>
    public IReadOnlyList<DuplicateGroup> Duplicates()
    {
        try
        {
            return LibraryDuplicates.Find(root);
        }
        catch (IndexException e)
        {
            throw new LibraryException(e.Message);
        }
    }

    /// <summary>
    /// Works that probably are one book (F-DUP-3): nearly the same text, or very similar vectors. For manual review only; pairs marked
    /// as not duplicates (F-DUP-6) are left out.
    /// </summary>
    public ProbablePairsResult FindProbableDuplicates()
    {
        try
        {
            var found = Shelf.Semantic.ProbableDuplicates.Find(root, settings.Grouping);
            return new ProbablePairsResult(
                found.Pairs.Count,
                found.Note,
                [.. found.Pairs.Select(p => new ProbablePair(p.Hamming, p.Cosine is { } c ? Math.Round(c, 4) : null, Entry(p.A), Entry(p.B)))]);
        }
        catch (Exception e) when (e is IndexException or Shelf.Semantic.VectorStoreException)
        {
            throw new LibraryException(e.Message);
        }

        static ProbableEntry Entry(ProbableWork work) => new(work.Id.ToString(), work.Title, [.. work.Authors], work.Folder);
    }

    /// <summary>Marks two works as not duplicates (F-DUP-6) if they are a probable pair now. Nothing moves.</summary>
    public DuplicateOutcome IgnoreProbableDuplicates(string a, string b)
    {
        if (!Ulid.TryParse(a, out var first) || !Ulid.TryParse(b, out var second))
        {
            throw new LibraryException("a and b must be the IDs of two works (26 characters each)");
        }

        if (first == second)
        {
            throw new LibraryException("a and b must be two different works");
        }

        // An ID that names no work is a 404.
        Find(a);
        Find(b);
        lock (_writeGate)
        {
            try
            {
                // The pair is looked up again now: the library may have changed since the list was shown.
                var pair = Shelf.Semantic.ProbableDuplicates.Find(root, settings.Grouping).Pairs
                    .FirstOrDefault(p => (p.A.Id == first && p.B.Id == second) || (p.A.Id == second && p.B.Id == first))
                    ?? throw new LibraryException("These two works are not probable duplicates (any more)", LibraryErrorKind.Conflict);
                var session = LibraryDuplicates.Begin(root);
                try
                {
                    session.MarkNotDuplicates(pair);
                }
                finally
                {
                    session.Finish();
                }

                return new DuplicateOutcome(0, []);
            }
            catch (Exception e) when (e is LibraryEditException or SidecarException or JournalException or IndexException or Shelf.Semantic.VectorStoreException)
            {
                throw new LibraryException(e.Message);
            }
        }
    }

    /// <summary>Moves every other file of the group of this file to the trash.</summary>
    public DuplicateOutcome KeepDuplicate(string fileId) =>
        ActOnDuplicate(fileId, (session, group, index) => session.Resolve(group, index));

    /// <summary>Moves this file to the trash. The group has at least one more file, which stays.</summary>
    public DuplicateOutcome TrashDuplicate(string fileId) =>
        ActOnDuplicate(
            fileId,
            (session, group, index) =>
            {
                // Resolve keeps one file and trashes the rest: a group of this file and one other trashes this file only.
                var other = group.Files[index == 0 ? 1 : 0];
                return session.Resolve(new DuplicateGroup(group.Kind, group.Format, [group.Files[index], other], Keep: 1), keep: 1);
            });

    /// <summary>Marks every pair of the group of this file as not duplicates (F-DUP-6). Nothing moves.</summary>
    public DuplicateOutcome IgnoreDuplicates(string fileId) =>
        ActOnDuplicate(
            fileId,
            (session, group, _) =>
            {
                session.MarkNotDuplicates(group);
                return 0;
            });

    private DuplicateOutcome ActOnDuplicate(string fileId, Func<DuplicateSession, DuplicateGroup, int, int> act)
    {
        // A malformed ID is a bad request; an ID that names nothing is a 404.
        if (!FileIds.TryParse(fileId, out _, out _))
        {
            throw new LibraryException($"'{fileId}' is not the ID of a file (the ID of a work, a tilde and the file name)");
        }

        var file = ResolveFile(fileId);
        lock (_writeGate)
        {
            try
            {
                // The group is looked up again now: the files may have changed since the list was shown.
                var group = LibraryDuplicates.Find(root)
                    .FirstOrDefault(g => g.Files.Any(f => f.Work == file.Work && f.Name == file.Name))
                    ?? throw new LibraryException($"'{file.Name}' is not part of a group of duplicates (any more)", LibraryErrorKind.Conflict);
                var index = group.Files.ToList().FindIndex(f => f.Work == file.Work && f.Name == file.Name);
                var session = LibraryDuplicates.Begin(root);
                try
                {
                    var trashed = act(session, group, index);
                    return new DuplicateOutcome(trashed, [.. session.WorksRemoved.Select(w => w.ToString())]);
                }
                finally
                {
                    session.Finish();
                }
            }
            catch (Exception e) when (e is LibraryEditException or SidecarException or JournalException or IndexException)
            {
                throw new LibraryException(e.Message);
            }
        }
    }

    /// <summary>
    /// Moves a file of a work into a work of its own, described by the file's own metadata (F-GRP-4), as <c>shelf split</c> does.
    /// The two works are marked as not duplicates. Like the CLI, this cannot be undone.
    /// </summary>
    /// <exception cref="LibraryException">NotFound: no such work or file. Conflict: the work has only this file.</exception>
    public SplitOutcome SplitFile(string fileId)
    {
        // A malformed ID is a bad request; an ID that names nothing is a 404.
        if (!FileIds.TryParse(fileId, out _, out _))
        {
            throw new LibraryException($"'{fileId}' is not the ID of a file (the ID of a work, a tilde and the file name)");
        }

        var file = ResolveFile(fileId);
        var work = Find(file.Work.ToString());
        lock (_writeGate)
        {
            try
            {
                if (ReadSidecar(work).Files.Count < 2)
                {
                    throw new LibraryException($"'{work.Title}' has only one file; there is nothing to split off", LibraryErrorKind.Conflict);
                }

                var result = LibraryEditor.Split(root, work, file.Name, root);

                // The new work has no cover yet. A file without one is no reason to fail a split that happened.
                LibraryCovers.FillWorks(root, [result.NewWork]);
                return new SplitOutcome(result.Source.ToString(), result.NewWork.ToString(), result.File, result.Folder);
            }
            catch (Exception e) when (e is LibraryEditException or SidecarException or IndexException)
            {
                throw new LibraryException(e.Message);
            }
        }
    }

    /// <summary>
    /// Merges the work <paramref name="source"/> into <paramref name="target"/> (F-GRP-4), as <c>shelf merge</c> does: the files move over, the
    /// target's metadata wins and the source fills its gaps. Like the CLI, this cannot be undone.
    /// </summary>
    /// <exception cref="LibraryException">NotFound: no such work. Conflict: both name the same work.</exception>
    public MergeOutcome MergeWorks(string target, string source)
    {
        foreach (var id in new[] { target, source })
        {
            if (!WorkLookup.LooksLikeId(id))
            {
                throw new LibraryException($"'{id}' is not the ID of a work");
            }
        }

        var into = Find(target);
        var from = Find(source);
        lock (_writeGate)
        {
            try
            {
                if (into.Id == from.Id)
                {
                    throw new LibraryException("cannot merge a work into itself", LibraryErrorKind.Conflict);
                }

                var result = LibraryEditor.Merge(root, into, from);
                return new MergeOutcome(result.Target.ToString(), result.Source.ToString(), result.MovedFiles, result.LeftoverFolder);
            }
            catch (Exception e) when (e is LibraryEditException or SidecarException or IndexException)
            {
                throw new LibraryException(e.Message);
            }
        }
    }

    // ---- resources ----

    /// <summary>Metadata of a work, without the table of contents (see <see cref="GetToc"/>).</summary>
    public BookDetails GetBookMetadata(string id) => Details(Find(id), withToc: false);

    public TocItem[] GetToc(string id) => Details(Find(id), withToc: true).Toc;

    // ---- helpers ----

    private float[] EmbedQuery(VectorStore store, string text)
    {
        var name = settings.Semantic.Model;
        if (store.Model != name)
        {
            throw new LibraryException(
                $"the vectors in this index were made with {store.Model} ({store.Dimensions} dimensions), not {name}; run: shelf reindex --embeddings to make them again");
        }

        var model = semantic.Catalog.Find(name)
            ?? throw new LibraryException($"unknown model '{name}'; known models: {string.Join(", ", semantic.Catalog.Models.Select(m => m.Name))}");
        var folder = ModelInstaller.FolderFor(root, model.Name);
        if (!ModelInstaller.IsInstalled(model, folder))
        {
            throw new LibraryException($"{name} is not installed; run: shelf model install");
        }

        return semantic.EmbedQuery(folder, text);
    }

    private TagChange ChangeTags(string id, string[] tags, Func<string, string, IReadOnlyCollection<Ulid>, TagResult> change)
    {
        if (tags.Length == 0)
        {
            throw new LibraryException("give at least one tag");
        }

        // All names are checked first: a refused tag must not leave the other ones applied.
        var names = new List<string>();
        foreach (var tag in tags)
        {
            if (!TagNames.TryNormalize(tag, out var name, out var error))
            {
                throw new LibraryException($"tag '{tag}': {error}");
            }

            if (!names.Contains(name))
            {
                names.Add(name);
            }
        }

        var work = Find(id);
        lock (_writeGate)
        {
            var changed = 0;
            try
            {
                foreach (var name in names)
                {
                    changed += change(root, name, [work.Id]).Changed;
                }
            }
            catch (Exception e) when (e is LibraryEditException or SidecarException or JournalException or IndexException)
            {
                throw new LibraryException(e.Message);
            }

            return new TagChange(work.Id.ToString(), work.Title, [.. LibraryEditor.Read(root, work).Tags], changed);
        }
    }

    private LibraryWork Find(string id)
    {
        try
        {
            return WorkLookup.Resolve(root, id, root);
        }
        catch (LibraryEditException e)
        {
            throw new LibraryException(e.Message, e.NotFound ? LibraryErrorKind.NotFound : LibraryErrorKind.Invalid);
        }
        catch (IndexException e)
        {
            throw new LibraryException(e.Message);
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
            files.Add(new BookFile(entry.Name, System.IO.Path.GetExtension(entry.Name).TrimStart('.').ToLowerInvariant(), info.Exists ? info.Length : 0, pages.GetValueOrDefault(entry.Name), entry.Progress is { } progress ? Position(progress) : null));
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
            sidecar.Edition,
            sidecar.CiteKey,
            sidecar.Description,
            sidecar.Rating,
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
