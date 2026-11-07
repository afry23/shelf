using System.Globalization;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using Shelf.Core.Model;
using Shelf.Core.Review;
using Shelf.Core.Search;
using Shelf.Index;
using Shelf.Semantic;

namespace Shelf.Library;

/// <summary>A work (or file) that a check names, with what is wrong with it.</summary>
public sealed record DoctorExample(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Id,
    string Title,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Detail);

/// <summary>
/// The result of one check (F-DOC-1). <paramref name="Status"/> is <c>ok</c>, <c>note</c> (could be better), <c>warning</c> (needs
/// attention) or <c>error</c> (data is broken). <paramref name="Count"/> is how many things are affected, <paramref name="Examples"/> names
/// the first few, <paramref name="Hint"/> says what to do.
/// </summary>
public sealed record DoctorCheck(
    string Id,
    string Status,
    string Summary,
    int Count,
    DoctorExample[] Examples,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Hint);

public sealed record DoctorReport(int Works, int Errors, int Warnings, int Notes, DoctorCheck[] Checks);

/// <summary>
/// The health check of a library (F-DOC-1, F-DOC-2): reads the sidecars, the files and the index and reports what is broken or incomplete,
/// and with which command to fix it. Nothing is written.
/// </summary>
public static class LibraryDoctor
{
    public const int MaxExamples = 5;

    private static readonly EnumerationOptions Recursive = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
        MatchCasing = MatchCasing.CaseSensitive,
    };

    private sealed record Found(string Directory, WorkSidecar Sidecar);

    public static DoctorReport Run(string root)
    {
        ArgumentException.ThrowIfNullOrEmpty(root);
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var checks = new List<DoctorCheck>();

        using var connection = OpenIndex(root, out var indexProblem, out var indexedWorks);
        checks.Add(connection is null
            ? Check("index", "error", $"{indexProblem}", 1, [], "shelf reindex")
            : Check("index", "ok", $"usable, {Plural(indexedWorks, "work", "works")}", 0, [], null));

        var sidecarFiles = Directory.EnumerateFiles(root, SidecarStore.FileName, Recursive).Order(StringComparer.Ordinal).ToList();
        var works = new List<Found>();
        var broken = new List<DoctorExample>();
        foreach (var path in sidecarFiles)
        {
            var directory = Path.GetDirectoryName(path)!;
            var shown = Path.GetRelativePath(root, directory);
            try
            {
                works.Add(new Found(directory, SidecarStore.Read(path)));
            }
            catch (SidecarException e)
            {
                broken.Add(new DoctorExample(null, shown, $"{SidecarStore.FileName}: {e.Message}"));
                continue;
            }

            var annotations = Path.Combine(directory, AnnotationsFormat.FileName);
            if (File.Exists(annotations))
            {
                try
                {
                    AnnotationsStore.Read(annotations);
                }
                catch (SidecarException e)
                {
                    broken.Add(new DoctorExample(null, shown, $"{AnnotationsFormat.FileName}: {e.Message}"));
                }
            }
        }

        checks.Add(broken.Count == 0
            ? Check("sidecars", "ok", $"all {works.Count} are valid", 0, [], null)
            : Check("sidecars", "error", $"{Plural(broken.Count, "file is", "files are")} not valid", broken.Count, broken, "fix the file by hand or restore it from a backup; shelf does not change a file it cannot read"));

        var missing = new List<DoctorExample>();
        foreach (var work in works)
        {
            foreach (var file in work.Sidecar.Files.Where(f => !File.Exists(Path.Combine(work.Directory, f.Name))))
            {
                missing.Add(new DoctorExample(work.Sidecar.Id.ToString(), work.Sidecar.Title, file.Name));
            }
        }

        checks.Add(missing.Count == 0
            ? Check("files", "ok", "every file named in a sidecar exists", 0, [], null)
            : Check("files", "error", $"{Plural(missing.Count, "file named in a sidecar is", "files named in sidecars are")} missing", missing.Count, missing, "restore the file from a backup, or remove it from the work (shelf delete <work> <file>)"));

        checks.Add(IndexSync(root, connection, works));
        checks.Add(Metadata(works));
        checks.Add(Covers(works));
        checks.Add(CiteKeys(works));
        checks.Add(Links(works));
        checks.Add(connection is null ? Skipped("text") : Text(connection));
        checks.Add(Review(root));
        checks.Add(connection is null ? Skipped("duplicates") : Duplicates(root));
        if (connection is not null && Embeddings(connection) is { } embeddings)
        {
            checks.Add(embeddings);
        }

        checks.Add(Searches(root));

        return new DoctorReport(
            sidecarFiles.Count,
            checks.Count(c => c.Status == "error"),
            checks.Count(c => c.Status == "warning"),
            checks.Count(c => c.Status == "note"),
            [.. checks]);
    }

    private static SqliteConnection? OpenIndex(string root, out string? problem, out int works)
    {
        problem = null;
        works = 0;
        var path = IndexDatabase.PathFor(root);
        if (!File.Exists(path))
        {
            problem = "there is no index";
            return null;
        }

        SqliteConnection? connection = null;
        try
        {
            connection = IndexDatabase.OpenRead(path);
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT count(*) FROM works";
            works = (int)(long)command.ExecuteScalar()!;
            return connection;
        }
        catch (Exception e) when (e is IndexException or SqliteException)
        {
            connection?.Dispose();
            problem = $"the index cannot be read: {e.Message}";
            return null;
        }
    }

    private static DoctorCheck IndexSync(string root, SqliteConnection? connection, List<Found> works)
    {
        if (connection is null)
        {
            return Skipped("index-sync");
        }

        var indexed = new Dictionary<string, string>(StringComparer.Ordinal);
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT id, title FROM works";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                indexed[reader.GetString(0)] = reader.GetString(1);
            }
        }

        var onDisk = works.ToDictionary(w => w.Sidecar.Id.ToString(), w => w, StringComparer.Ordinal);
        var examples = new List<DoctorExample>();
        examples.AddRange(onDisk.Where(w => !indexed.ContainsKey(w.Key)).Select(w => new DoctorExample(w.Key, w.Value.Sidecar.Title, "not in the index yet")));
        examples.AddRange(indexed.Where(i => !onDisk.ContainsKey(i.Key)).Select(i => new DoctorExample(i.Key, i.Value, "in the index, but its folder is gone")));
        return examples.Count == 0
            ? Check("index-sync", "ok", "the index matches the folders", 0, [], null)
            : Check("index-sync", "warning", $"{Plural(examples.Count, "work differs", "works differ")} between the folders and the index", examples.Count, examples, "shelf scan (or shelf reindex to rebuild the index)");
    }

    private static DoctorCheck Metadata(List<Found> works)
    {
        var noYear = works.Where(w => w.Sidecar.Year is null).ToList();
        var noLanguage = works.Where(w => string.IsNullOrWhiteSpace(w.Sidecar.Language)).ToList();
        var gaps = new List<string>();
        foreach (var (list, what) in new[] { (noYear, "year"), (noLanguage, "language") })
        {
            if (list.Count > 0)
            {
                gaps.Add($"{(list.Count == 1 ? "1 work has" : $"{list.Count} works have")} no {what}");
            }
        }

        if (gaps.Count == 0)
        {
            return Check("metadata", "ok", "every work has a year and a language", 0, [], null);
        }

        var examples = noYear.Select(w => (w, "year"))
            .Concat(noLanguage.Select(w => (w, "language")))
            .GroupBy(x => x.w.Sidecar.Id)
            .Select(g => new DoctorExample(g.Key.ToString(), g.First().w.Sidecar.Title, "no " + string.Join(", no ", g.Select(x => x.Item2))))
            .ToList();
        return Check(
            "metadata",
            "warning",
            string.Join(", ", gaps),
            examples.Count,
            examples,
            "shelf enrich <work> looks them up online; or set them with shelf edit <work> --language en, for many works: shelf edit --query '<search>' --language en --apply");
    }

    private static DoctorCheck Covers(List<Found> works)
    {
        var without = works
            .Where(w => w.Sidecar.Cover is null || !File.Exists(Path.Combine(w.Directory, w.Sidecar.Cover)))
            .Select(w => new DoctorExample(w.Sidecar.Id.ToString(), w.Sidecar.Title, null))
            .ToList();
        return without.Count == 0
            ? Check("covers", "ok", "every work has a cover", 0, [], null)
            : Check("covers", "note", $"{Plural(without.Count, "work has", "works have")} no cover", without.Count, without, "shelf covers");
    }

    private static DoctorCheck CiteKeys(List<Found> works)
    {
        var papers = works.Where(w => w.Sidecar.Kind == WorkKind.Paper).ToList();
        var without = papers
            .Where(w => string.IsNullOrWhiteSpace(w.Sidecar.CiteKey))
            .Select(w => new DoctorExample(w.Sidecar.Id.ToString(), w.Sidecar.Title, null))
            .ToList();
        return without.Count == 0
            ? Check("cite-keys", "ok", papers.Count == 0 ? "no papers" : "every paper has a cite key", 0, [], null)
            : Check("cite-keys", "note", $"{Plural(without.Count, "paper has", "papers have")} no cite key", without.Count, without, "shelf citekeys");
    }

    private static DoctorCheck Links(List<Found> works)
    {
        var known = works.Select(w => w.Sidecar.Id).ToHashSet();
        var dangling = works
            .SelectMany(w => w.Sidecar.Links.Where(l => !known.Contains(l.Work)).Select(l => new DoctorExample(w.Sidecar.Id.ToString(), w.Sidecar.Title, $"link to {l.Work} (no such work)")))
            .ToList();
        return dangling.Count == 0
            ? Check("links", "ok", "every link points to a work", 0, [], null)
            : Check("links", "warning", $"{Plural(dangling.Count, "link points", "links point")} to a work that does not exist", dangling.Count, dangling, "remove the link from the book.json of the work; shelf ignores it until then");
    }

    private static DoctorCheck Text(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT w.id, w.title, f.rel_path, f.text_status
            FROM files f JOIN works w ON w.id = f.work_id
            WHERE f.role = 'book' AND f.text_status IN ('failed', 'none', 'pending')
            ORDER BY f.text_status, w.title, f.rel_path
            """;
        var failed = new List<DoctorExample>();
        var none = new List<DoctorExample>();
        var pending = new List<DoctorExample>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                var example = new DoctorExample(reader.GetString(0), reader.GetString(1), Path.GetFileName(reader.GetString(2)));
                (reader.GetString(3) switch { "failed" => failed, "none" => none, _ => pending }).Add(example);
            }
        }

        if (failed.Count + none.Count + pending.Count == 0)
        {
            return Check("text", "ok", "the text of every file is indexed", 0, [], null);
        }

        var parts = new List<string>();
        if (failed.Count > 0)
        {
            parts.Add($"{Plural(failed.Count, "file cannot be read", "files cannot be read")}");
        }

        if (none.Count > 0)
        {
            parts.Add($"{Plural(none.Count, "file has", "files have")} no text layer (found by metadata only)");
        }

        if (pending.Count > 0)
        {
            parts.Add($"{Plural(pending.Count, "file is", "files are")} not read yet");
        }

        var hint = failed.Count > 0
            ? "check or replace the file that cannot be read (shelf add <work> <file> --replace <old file>); shelf scan reads files again"
            : pending.Count > 0 ? "shelf scan" : "there is no OCR; the file stays in the library and is found by title and author";
        return Check("text", failed.Count > 0 ? "warning" : "note", string.Join(", ", parts), failed.Count + none.Count + pending.Count, [.. failed, .. none, .. pending], hint);
    }

    private static DoctorCheck Review(string root)
    {
        try
        {
            var entries = ReviewQueue.Load(ReviewQueue.PathFor(root)).Entries;
            return entries.Count == 0
                ? Check("review", "ok", "the review queue is empty", 0, [], null)
                : Check("review", "warning", $"{Plural(entries.Count, "entry waits", "entries wait")} in the review queue", entries.Count, [.. entries.Select(e => new DoctorExample(e.Id.ToString(), e.Message, e.Kind.ToString()))], "shelf review");
        }
        catch (ReviewQueueException e)
        {
            return Check("review", "warning", $"the review queue cannot be read: {e.Message}", 1, [], "shelf reindex reports it; the file is never changed by shelf");
        }
    }

    private static DoctorCheck Duplicates(string root)
    {
        try
        {
            var groups = LibraryDuplicates.Find(root);
            return groups.Count == 0
                ? Check("duplicates", "ok", "no identical files", 0, [], null)
                : Check(
                    "duplicates",
                    "warning",
                    $"{Plural(groups.Count, "group of identical files", "groups of identical files")}",
                    groups.Count,
                    [.. groups.Select(g => new DoctorExample(null, g.Files[0].Title, string.Join(", ", g.Files.Select(f => f.Path))))],
                    "shelf dupes");
        }
        catch (IndexException e)
        {
            return Check("duplicates", "note", $"not checked: {e.Message}", 0, [], null);
        }
    }

    private static DoctorCheck? Embeddings(SqliteConnection connection)
    {
        VectorStore? store;
        try
        {
            store = VectorStore.OpenExisting(connection);
        }
        catch (VectorStoreException)
        {
            return null;
        }

        if (store is null)
        {
            return null; // semantic search is not set up: nothing to report
        }

        var coverage = SemanticSearch.Coverage(connection, store);
        return coverage.IsComplete
            ? Check("embeddings", "ok", "every file has its embeddings", 0, [], null)
            : Check(
                "embeddings",
                "warning",
                $"{Plural(coverage.PendingFiles, "file has", "files have")} no embeddings yet, {Plural(coverage.WorksWithoutVector, "work has", "works have")} no work embedding",
                coverage.PendingFiles + coverage.WorksWithoutVector,
                [],
                "shelf reindex --embeddings");
    }

    private static DoctorCheck Searches(string root)
    {
        SavedSearches set;
        try
        {
            set = SavedSearches.Load(root);
        }
        catch (SavedSearchException e)
        {
            return Check("searches", "warning", $"the saved searches cannot be read: {e.Message}", 1, [], "fix or remove .shelf/searches.json");
        }

        var invalid = new List<DoctorExample>();
        foreach (var item in set.Items)
        {
            try
            {
                QueryParser.Parse(item.Query);
            }
            catch (QueryException e)
            {
                invalid.Add(new DoctorExample(null, item.Name, e.Message));
            }
        }

        return invalid.Count == 0
            ? Check("searches", "ok", set.Items.Count == 0 ? "no saved searches" : $"all {set.Items.Count} saved searches are valid", 0, [], null)
            : Check("searches", "warning", $"{Plural(invalid.Count, "saved search is", "saved searches are")} not valid any more", invalid.Count, invalid, "shelf searches add <name> <query> --replace, or shelf searches rm <name>");
    }

    private static DoctorCheck Skipped(string id) => Check(id, "note", "not checked: there is no usable index", 0, [], null);

    private static DoctorCheck Check(string id, string status, string summary, int count, IReadOnlyList<DoctorExample> examples, string? hint) =>
        new(id, status, summary, count, [.. examples.Take(MaxExamples)], hint);

    private static string Plural(int count, string one, string many) =>
        count.ToString(CultureInfo.InvariantCulture) + " " + (count == 1 ? one : many);
}
