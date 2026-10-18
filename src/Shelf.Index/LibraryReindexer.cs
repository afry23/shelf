using Microsoft.Data.Sqlite;
using Shelf.Core.Model;
using Shelf.Core.Review;

namespace Shelf.Index;

/// <summary>Something in the library that reindex could not use; the rest is still indexed.</summary>
public sealed record ReindexProblem(string Path, string Message);

public sealed record ReindexResult(int Works, int Files, IReadOnlyList<ReindexProblem> Problems);

/// <summary>
/// Rebuilds the metadata part of the index from the file system and the <c>book.json</c> sidecars (F-LIB-6).
/// Everything happens in one transaction: concurrent readers see the old index until the new one is complete,
/// and a failure leaves the old index untouched.
/// </summary>
/// <remarks>
/// Not rebuilt here: hashes, page counts, SimHash and chunks (they come from extracting the files, slice
/// <c>m1-import-scan</c>). The review queue is mirrored from <c>.shelf/review.json</c>; a damaged queue file is
/// reported as a problem and mirrored as empty, the file itself is never touched.
/// </remarks>
public static class LibraryReindexer
{
    internal static readonly EnumerationOptions Recursive = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        // Skips hidden entries, which on Linux and macOS means dot-names: .shelf, .git, ...
        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
        MatchCasing = MatchCasing.CaseSensitive,
    };

    /// <param name="root">The library root.</param>
    /// <param name="includeContent">
    /// Also read every file (hash, text, fingerprint) as <see cref="LibraryScanner"/> does. Without it only the
    /// metadata of the sidecars is rebuilt, which is fast; a later scan fills in the content.
    /// </param>
    /// <exception cref="IndexException">The index cannot be written.</exception>
    public static ReindexResult Rebuild(string root, bool includeContent = true)
    {
        ArgumentException.ThrowIfNullOrEmpty(root);
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));

        var problems = new List<ReindexProblem>();
        var works = Discover(root, problems);
        var review = LoadReview(root, problems);

        using var connection = OpenIndex(IndexDatabase.PathFor(root));
        int fileCount = 0;
        using (var writer = new IndexWriter(connection))
        using (var transaction = writer.BeginTransaction())
        {
            Reset(connection);
            foreach (var work in works)
            {
                writer.InsertWork(work.Sidecar, work.DirPath, work.SidecarModified);
                foreach (var file in work.Files)
                {
                    writer.InsertFile(work.Sidecar.Id.ToString(), file.Entry.Name, file.RelPath, file.Size, file.Mtime, file.Entry.Progress);
                    fileCount++;
                }
            }

            writer.ReplaceReviewMirror(review);
            transaction.Commit();
        }

        if (includeContent)
        {
            LibraryScanner.Run(connection, root, problems, reconcileWorks: false);
        }

        IndexDatabase.Checkpoint(connection);
        return new ReindexResult(works.Count, fileCount, problems);
    }

    private static ReviewQueue LoadReview(string root, List<ReindexProblem> problems)
    {
        var path = ReviewQueue.PathFor(root);
        try
        {
            return ReviewQueue.Load(path);
        }
        catch (ReviewQueueException e)
        {
            problems.Add(new ReindexProblem(path, e.Message));
            return new ReviewQueue();
        }
    }

    private static List<IndexedWork> Discover(string root, List<ReindexProblem> problems)
    {
        var sidecarPaths = Directory.EnumerateFiles(root, SidecarStore.FileName, Recursive).Order(StringComparer.Ordinal).ToList();
        var works = new List<IndexedWork>();
        var ids = new Dictionary<Ulid, string>();
        foreach (var sidecarPath in sidecarPaths)
        {
            WorkSidecar sidecar;
            try
            {
                sidecar = SidecarStore.Read(sidecarPath);
            }
            catch (SidecarException e)
            {
                problems.Add(new ReindexProblem(sidecarPath, e.Message));
                continue;
            }

            if (ids.TryGetValue(sidecar.Id, out var first))
            {
                problems.Add(new ReindexProblem(sidecarPath, $"{sidecarPath}: duplicate work ID {sidecar.Id}, already used by {first}"));
                continue;
            }

            ids.Add(sidecar.Id, sidecarPath);
            var directory = Path.GetDirectoryName(sidecarPath)!;
            var files = new List<IndexedFile>();
            foreach (var entry in sidecar.Files)
            {
                var info = new FileInfo(Path.Combine(directory, entry.Name));
                if (!info.Exists)
                {
                    problems.Add(new ReindexProblem(sidecarPath, $"{sidecarPath}: {entry.Name}: file does not exist"));
                    continue;
                }

                files.Add(new IndexedFile(entry, RelativePath(root, info.FullName), info.Length, IndexWriter.Mtime(info)));
            }

            if (files.Count > 0)
            {
                works.Add(new IndexedWork(sidecar, RelativePath(root, directory), File.GetLastWriteTimeUtc(sidecarPath), files));
            }
        }

        return works;
    }

    private static void Reset(SqliteConnection connection)
    {
        if (IndexDatabase.GetVersion(connection) != IndexSchema.Version)
        {
            // Another version, or a new empty file: nothing in it is worth keeping, the index is a cache.
            IndexDatabase.DropSchema(connection);
            IndexDatabase.CreateSchema(connection);
            return;
        }

        foreach (var table in IndexSchema.DataTablesChildrenFirst)
        {
            IndexDatabase.Execute(connection, $"DELETE FROM {table}");
        }

        IndexDatabase.Execute(connection, "DELETE FROM works_fts");
    }

    /// <summary>Opens the index; a file that is not a database at all is a cache that went bad, so it is replaced.</summary>
    private static SqliteConnection OpenIndex(string path)
    {
        try
        {
            return IndexDatabase.OpenForRebuild(path);
        }
        // Only for "not a database" (26) and "corrupt" (11): a busy or locked index (5, 6) belongs to a running process.
        catch (IndexException e) when (e.InnerException is SqliteException { SqliteErrorCode: 26 or 11 } && File.Exists(path))
        {
            foreach (var file in new[] { path, path + "-wal", path + "-shm" })
            {
                File.Delete(file);
            }

            return IndexDatabase.OpenForRebuild(path);
        }
    }

    internal static string RelativePath(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
        return relative;
    }

    private sealed record IndexedFile(SidecarFile Entry, string RelPath, long Size, long Mtime);

    private sealed record IndexedWork(WorkSidecar Sidecar, string DirPath, DateTime SidecarModified, List<IndexedFile> Files);
}
