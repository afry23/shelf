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
public sealed class LibraryService(string root, ShelfSettings settings, SemanticBackend semantic)
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
            sidecar.Edition,
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
