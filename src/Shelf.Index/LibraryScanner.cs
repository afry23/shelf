using Microsoft.Data.Sqlite;
using Shelf.Core.Model;
using Shelf.Core.Review;
using Shelf.Extraction;

namespace Shelf.Index;

/// <summary>What a scan found and did.</summary>
/// <param name="Works">Works in the index after the scan.</param>
/// <param name="Files">Files in the index after the scan.</param>
/// <param name="UnchangedFiles">Files whose size and time were as recorded, plus touched files whose hash was the same.</param>
/// <param name="TouchedFiles">Files with a new time but the same hash: not read again.</param>
/// <param name="Untracked">Supported files outside any work folder, as library-relative paths.</param>
public sealed record ScanResult(
    int Works,
    int Files,
    int NewWorks,
    int UpdatedWorks,
    int RemovedWorks,
    int NewFiles,
    int ChangedFiles,
    int TouchedFiles,
    int RemovedFiles,
    int UnchangedFiles,
    IReadOnlyList<string> Untracked,
    IReadOnlyList<ReindexProblem> Problems);

/// <summary>
/// Brings the index in line with the folders and sidecars of a library (F-LIB-7). Only what changed is looked
/// at: a work whose sidecar has the recorded time is not read, a file whose size and time are as recorded is not
/// hashed, a file with a new time but the same hash is not extracted again.
/// </summary>
/// <remarks>
/// A file that appears in a work folder (next to a <c>book.json</c>) joins that work and is added to its sidecar.
/// A file anywhere else cannot get a sidecar of its own without moving it, so it is only reported (use import).
/// A sidecar is never changed otherwise; a file that disappeared leaves the index but stays named in the sidecar.
/// </remarks>
public static class LibraryScanner
{
    /// <exception cref="IndexException">There is no index, or it has another schema version (run reindex).</exception>
    public static ScanResult Scan(string root)
    {
        ArgumentException.ThrowIfNullOrEmpty(root);
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        using var connection = IndexDatabase.OpenWrite(IndexDatabase.PathFor(root));
        var result = Run(connection, root, [], reconcileWorks: true);
        IndexDatabase.Checkpoint(connection);
        return result;
    }

    /// <param name="problems">Collects what could not be used; the same list is in the result.</param>
    /// <param name="reconcileWorks">False right after a rebuild, which has just read every sidecar.</param>
    internal static ScanResult Run(SqliteConnection connection, string root, List<ReindexProblem> problems, bool reconcileWorks)
    {
        using var writer = new IndexWriter(connection);
        int newWorks = 0, updatedWorks = 0, removedWorks = 0, removedFiles = 0;

        if (reconcileWorks)
        {
            using var transaction = writer.BeginTransaction();
            (newWorks, updatedWorks, removedWorks, removedFiles) = ReconcileWorks(connection, writer, root, problems);
            transaction.Commit();
        }

        var works = LoadWorkDirectories(connection);
        var rows = LoadFiles(connection);

        // ---- which files need to be looked at ----
        var needs = new List<Need>();
        var unchanged = 0;
        var gone = new List<long>();
        foreach (var row in rows)
        {
            var info = new FileInfo(Path.Combine(root, row.RelPath));
            if (!info.Exists)
            {
                gone.Add(row.Id);
                continue;
            }

            var mtime = IndexWriter.Mtime(info);
            if (row.Sha256 is null)
            {
                needs.Add(new Need(row, info.FullName, KnownSha: null, IsNew: true));
            }
            else if (info.Length != row.Size || mtime != row.Mtime)
            {
                needs.Add(new Need(row, info.FullName, row.Sha256, IsNew: false));
            }
            else
            {
                unchanged++;
            }
        }

        var untracked = new List<string>();
        var tracked = rows.Select(r => r.RelPath).ToHashSet(StringComparer.Ordinal);
        var attached = new List<(string WorkId, string Name, string RelPath, string FullPath)>();
        foreach (var path in Directory.EnumerateFiles(root, "*", LibraryReindexer.Recursive).Order(StringComparer.Ordinal))
        {
            if (!BookExtractor.IsSupported(path))
            {
                continue;
            }

            var rel = LibraryReindexer.RelativePath(root, path);
            if (tracked.Contains(rel))
            {
                continue;
            }

            var directory = LibraryReindexer.RelativePath(root, Path.GetDirectoryName(path)!);
            if (works.TryGetValue(directory, out var workId))
            {
                attached.Add((workId, Path.GetFileName(path), rel, path));
            }
            else
            {
                untracked.Add(rel);
            }
        }

        // ---- apply ----
        var queue = LoadQueue(root, problems);
        var queueChanged = false;
        int newFiles = 0, changedFiles = 0, touched = 0;

        using (var transaction = writer.BeginTransaction())
        {
            foreach (var id in gone)
            {
                writer.DeleteFile(id);
                removedFiles++;
            }

            foreach (var group in attached.GroupBy(a => a.WorkId))
            {
                AttachToWork(writer, group.Key, [.. group], needs, problems);
            }

            transaction.Commit();
        }

        var items = needs.Select(n => (n.FullPath, n.KnownSha)).ToList();
        FileAnalyzer.ForEach(items, (index, analysis) =>
        {
            var need = needs[index];
            using var transaction = writer.BeginTransaction();
            if (analysis.Sha256 is null)
            {
                // Could not even be read: nothing new is known; the old row stays as it is.
                problems.Add(new ReindexProblem(need.FullPath, analysis.Error!.Message));
            }
            else if (!analysis.Extracted)
            {
                writer.UpdateFileStat(need.Row.Id, analysis.Size, analysis.Mtime);
                touched++;
            }
            else
            {
                writer.SetContent(need.Row.Id, analysis.Size, analysis.Mtime, analysis.Sha256, analysis.Book?.PageCount, analysis.Fingerprint, analysis.TextStatus, analysis.Chunks);
                if (need.IsNew)
                {
                    newFiles++;
                }
                else
                {
                    changedFiles++;
                }

                if (analysis.Error is not null)
                {
                    problems.Add(new ReindexProblem(need.FullPath, analysis.Error.Message));
                    queueChanged |= AddExtractionEntry(queue, need.Row.RelPath, analysis.Error);
                }
                else
                {
                    queueChanged |= RemoveExtractionEntries(queue, need.Row.RelPath);
                }
            }

            transaction.Commit();
        });

        // Nothing to write after a scan that found nothing: skip the transaction. The mirror of the queue is
        // refreshed when the queue changed or its size differs from the mirror (a hand-edited file); a reindex
        // always rewrites it.
        var mirrorStale = queueChanged || Count(connection, "review_queue") != queue.Entries.Count;
        var changedSomething = mirrorStale || newWorks + updatedWorks + removedWorks + newFiles + changedFiles + removedFiles > 0;
        if (changedSomething)
        {
            using var transaction = writer.BeginTransaction();
            writer.PruneTags();
            if (mirrorStale)
            {
                writer.ReplaceReviewMirror(queue);
            }

            transaction.Commit();
        }

        if (queueChanged)
        {
            try
            {
                queue.Save(ReviewQueue.PathFor(root));
            }
            catch (ReviewQueueException e)
            {
                problems.Add(new ReindexProblem(ReviewQueue.PathFor(root), e.Message));
            }
        }

        return new ScanResult(
            Count(connection, "works"),
            Count(connection, "files"),
            newWorks,
            updatedWorks,
            removedWorks,
            newFiles,
            changedFiles,
            touched,
            removedFiles,
            unchanged + touched,
            untracked,
            problems);
    }

    // ---- works ----

    private static (int New, int Updated, int Removed, int RemovedFiles) ReconcileWorks(
        SqliteConnection connection,
        IndexWriter writer,
        string root,
        List<ReindexProblem> problems)
    {
        var known = LoadWorks(connection);
        var byId = known.Values.ToDictionary(w => w.Id, StringComparer.Ordinal);
        var onDisk = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(root, SidecarStore.FileName, LibraryReindexer.Recursive).Order(StringComparer.Ordinal))
        {
            onDisk[LibraryReindexer.RelativePath(root, Path.GetDirectoryName(path)!)] = File.GetLastWriteTimeUtc(path);
        }

        int created = 0, updated = 0, removed = 0, removedFiles = 0;
        var seenIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (dir, modified) in onDisk)
        {
            var sidecarPath = Path.Combine(root, dir, SidecarStore.FileName);
            if (known.TryGetValue(dir, out var existing) && existing.UpdatedAt == IndexWriter.ModifiedStamp(modified))
            {
                seenIds.Add(existing.Id);
                continue;
            }

            WorkSidecar sidecar;
            try
            {
                sidecar = SidecarStore.Read(sidecarPath);
            }
            catch (SidecarException e)
            {
                problems.Add(new ReindexProblem(sidecarPath, e.Message));
                if (existing is not null)
                {
                    seenIds.Add(existing.Id); // keep what the index has until the sidecar is fixed
                }

                continue;
            }

            var id = sidecar.Id.ToString();
            if (!seenIds.Add(id))
            {
                problems.Add(new ReindexProblem(sidecarPath, $"{sidecarPath}: duplicate work ID {id}"));
                continue;
            }

            if (existing is not null && existing.Id != id)
            {
                // The ID in this folder's sidecar is another one than the indexed work's (edited by hand):
                // the old work is gone, the sidecar describes a new one.
                removedFiles += CountFiles(connection, existing.Id);
                writer.DeleteWork(existing.Id);
                removed++;
                byId.Remove(existing.Id);
            }

            if (byId.TryGetValue(id, out var current))
            {
                SyncWork(connection, writer, root, sidecar, dir, modified, problems, sidecarPath);
                updated++;
            }
            else
            {
                writer.InsertWork(sidecar, dir, modified);
                foreach (var entry in sidecar.Files)
                {
                    var info = new FileInfo(Path.Combine(root, dir, entry.Name));
                    if (!info.Exists)
                    {
                        problems.Add(new ReindexProblem(sidecarPath, $"{sidecarPath}: {entry.Name}: file does not exist"));
                        continue;
                    }

                    writer.InsertFile(id, entry.Name, IndexWriter.JoinRelative(dir, entry.Name), info.Length, IndexWriter.Mtime(info), entry.Progress);
                }

                created++;
            }
        }

        // Works of the index whose folder has no sidecar any more and that did not move elsewhere.
        foreach (var (dir, work) in known)
        {
            if (onDisk.ContainsKey(dir) || seenIds.Contains(work.Id))
            {
                continue;
            }

            removedFiles += CountFiles(connection, work.Id);
            writer.DeleteWork(work.Id);
            removed++;
        }

        return (created, updated, removed, removedFiles);
    }

    /// <summary>Replaces the metadata of a work and reconciles its file rows with the sidecar by file name.</summary>
    private static void SyncWork(
        SqliteConnection connection,
        IndexWriter writer,
        string root,
        WorkSidecar sidecar,
        string dir,
        DateTime modified,
        List<ReindexProblem> problems,
        string sidecarPath)
    {
        writer.UpdateWork(sidecar, dir, modified);
        var id = sidecar.Id.ToString();
        var rows = new Dictionary<string, (long Id, string RelPath)>(StringComparer.Ordinal);
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT id, rel_path FROM files WHERE work_id = $w";
            command.Parameters.AddWithValue("$w", id);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                rows[Path.GetFileName(reader.GetString(1))] = (reader.GetInt64(0), reader.GetString(1));
            }
        }

        var listed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in sidecar.Files)
        {
            listed.Add(entry.Name);
            var rel = IndexWriter.JoinRelative(dir, entry.Name);
            if (rows.TryGetValue(entry.Name, out var row))
            {
                if (row.RelPath != rel)
                {
                    writer.UpdateFilePath(row.Id, rel);
                }

                writer.SetProgress(row.Id, entry.Progress);
                continue;
            }

            var info = new FileInfo(Path.Combine(root, rel));
            if (!info.Exists)
            {
                problems.Add(new ReindexProblem(sidecarPath, $"{sidecarPath}: {entry.Name}: file does not exist"));
                continue;
            }

            writer.InsertFile(id, entry.Name, rel, info.Length, IndexWriter.Mtime(info), entry.Progress);
        }

        foreach (var (name, row) in rows)
        {
            if (!listed.Contains(name))
            {
                writer.DeleteFile(row.Id);
            }
        }
    }

    /// <summary>Files that appeared next to a sidecar: named in the sidecar, then indexed.</summary>
    private static void AttachToWork(
        IndexWriter writer,
        string workId,
        List<(string WorkId, string Name, string RelPath, string FullPath)> files,
        List<Need> needs,
        List<ReindexProblem> problems)
    {
        var dir = Path.GetDirectoryName(files[0].FullPath)!;
        var sidecarPath = Path.Combine(dir, SidecarStore.FileName);
        WorkSidecar sidecar;
        try
        {
            sidecar = SidecarStore.Read(sidecarPath);
        }
        catch (SidecarException e)
        {
            problems.Add(new ReindexProblem(sidecarPath, e.Message));
            return;
        }

        // Named in the sidecar already (a file that came back) or new (the sidecar gets the name).
        var named = sidecar.Files.Select(f => f.Name).ToHashSet(StringComparer.Ordinal);
        var added = files.Where(f => !named.Contains(f.Name)).ToList();
        if (added.Count > 0)
        {
            SidecarStore.Write(sidecarPath, sidecar with { Files = [.. sidecar.Files, .. added.Select(f => new SidecarFile(f.Name))] });
            writer.SetWorkModified(workId, File.GetLastWriteTimeUtc(sidecarPath));
        }

        foreach (var file in files)
        {
            var info = new FileInfo(file.FullPath);
            var fileId = writer.InsertFile(workId, file.Name, file.RelPath, info.Length, IndexWriter.Mtime(info), progress: null);
            needs.Add(new Need(new FileRow(fileId, file.RelPath, info.Length, 0, null), file.FullPath, KnownSha: null, IsNew: true));
        }
    }

    // ---- review queue ----

    private static ReviewQueue LoadQueue(string root, List<ReindexProblem> problems)
    {
        try
        {
            return ReviewQueue.Load(ReviewQueue.PathFor(root));
        }
        catch (ReviewQueueException e)
        {
            problems.Add(new ReindexProblem(ReviewQueue.PathFor(root), e.Message));
            return new ReviewQueue();
        }
    }

    private static bool AddExtractionEntry(ReviewQueue queue, string relPath, ExtractionException error)
    {
        if (queue.Entries.Any(e => e.Kind == ReviewKind.ExtractionError && e.File == relPath))
        {
            return false;
        }

        queue.Add(new ReviewEntry(Ulid.New(), ReviewKind.ExtractionError, DateTimeOffset.UtcNow, $"{error.KindName}: {error.Message}", relPath));
        return true;
    }

    private static bool RemoveExtractionEntries(ReviewQueue queue, string relPath)
    {
        var ids = queue.Entries.Where(e => e.Kind == ReviewKind.ExtractionError && e.File == relPath).Select(e => e.Id).ToList();
        foreach (var id in ids)
        {
            queue.Remove(id);
        }

        return ids.Count > 0;
    }

    // ---- queries ----

    private sealed record KnownWork(string Id, string DirPath, string UpdatedAt);

    private sealed record FileRow(long Id, string RelPath, long Size, long Mtime, string? Sha256);

    private sealed record Need(FileRow Row, string FullPath, string? KnownSha, bool IsNew);

    private static Dictionary<string, KnownWork> LoadWorks(SqliteConnection connection)
    {
        var works = new Dictionary<string, KnownWork>(StringComparer.Ordinal);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, dir_path, updated_at FROM works";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            works[reader.GetString(1)] = new KnownWork(reader.GetString(0), reader.GetString(1), reader.GetString(2));
        }

        return works;
    }

    private static Dictionary<string, string> LoadWorkDirectories(SqliteConnection connection) =>
        LoadWorks(connection).ToDictionary(w => w.Key, w => w.Value.Id, StringComparer.Ordinal);

    private static List<FileRow> LoadFiles(SqliteConnection connection)
    {
        var rows = new List<FileRow>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, rel_path, size, mtime, sha256 FROM files ORDER BY rel_path";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new FileRow(reader.GetInt64(0), reader.GetString(1), reader.GetInt64(2), reader.GetInt64(3), reader.IsDBNull(4) ? null : reader.GetString(4)));
        }

        return rows;
    }

    private static int Count(SqliteConnection connection, string table) => (int)(long)IndexDatabase.Scalar(connection, $"SELECT COUNT(*) FROM {table}")!;

    private static int CountFiles(SqliteConnection connection, string workId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM files WHERE work_id = $w";
        command.Parameters.AddWithValue("$w", workId);
        return (int)(long)command.ExecuteScalar()!;
    }
}
