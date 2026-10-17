using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Shelf.Core.Model;

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
/// <c>m1-import-scan</c>), and the review queue (its entry format comes with <c>m1-grouping</c>).
/// <c>title_norm</c> and <c>author_sort</c> are simple placeholders until <c>m1-grouping</c> defines the real rules.
/// </remarks>
public static class LibraryReindexer
{
    private static readonly EnumerationOptions Recursive = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        // Skips hidden entries, which on Linux and macOS means dot-names: .shelf, .git, ...
        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
        MatchCasing = MatchCasing.CaseSensitive,
    };

    /// <exception cref="IndexException">The index cannot be written.</exception>
    public static ReindexResult Rebuild(string root)
    {
        ArgumentException.ThrowIfNullOrEmpty(root);
        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));

        var problems = new List<ReindexProblem>();
        var works = Discover(root, problems);

        using var connection = OpenIndex(IndexDatabase.PathFor(root));
        int fileCount;
        using (var transaction = connection.BeginTransaction())
        {
            Reset(connection);
            fileCount = Insert(connection, works);
            transaction.Commit();
        }

        IndexDatabase.Checkpoint(connection);
        return new ReindexResult(works.Count, fileCount, problems);
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

                files.Add(new IndexedFile(entry, RelativePath(root, info.FullName), info.Length, new DateTimeOffset(info.LastWriteTimeUtc).ToUnixTimeSeconds()));
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

    private static int Insert(SqliteConnection connection, List<IndexedWork> works)
    {
        using var insertWork = Prepare(connection, """
            INSERT INTO works(id, title, title_norm, subtitle, authors_json, author_sort, language, year, isbn13, publisher,
                              series, series_index, edition, description, rating, read_status, cover_path, dir_path, added_at, updated_at)
            VALUES ($id, $title, $titleNorm, $subtitle, $authors, $authorSort, $language, $year, $isbn13, $publisher,
                    $series, $seriesIndex, $edition, $description, $rating, $status, $cover, $dir, $added, $updated)
            RETURNING seq
            """);
        using var insertFile = Prepare(connection,
            "INSERT INTO files(work_id, format, rel_path, size, mtime) VALUES ($work, $format, $path, $size, $mtime) RETURNING id");
        using var insertProgress = Prepare(connection,
            "INSERT INTO reading_progress(file_id, locator, updated_at) VALUES ($file, $locator, $updated)");
        using var insertTag = Prepare(connection, "INSERT OR IGNORE INTO tags(path) VALUES ($path)");
        using var selectTag = Prepare(connection, "SELECT id FROM tags WHERE path = $path");
        using var insertWorkTag = Prepare(connection, "INSERT OR IGNORE INTO work_tags(work_id, tag_id) VALUES ($work, $tag)");
        using var insertIgnore = Prepare(connection, "INSERT OR IGNORE INTO dupe_ignores(a_id, b_id, kind) VALUES ($a, $b, $kind)");
        using var insertFts = Prepare(connection,
            "INSERT INTO works_fts(rowid, title, subtitle, authors, description, tags) VALUES ($seq, $title, $subtitle, $authors, $description, $tags)");

        var tagIds = new Dictionary<string, long>(StringComparer.Ordinal);
        var fileCount = 0;
        foreach (var work in works)
        {
            var s = work.Sidecar;
            var id = s.Id.ToString();
            var dir = work.DirPath;

            Bind(insertWork, "$id", id);
            Bind(insertWork, "$title", s.Title);
            Bind(insertWork, "$titleNorm", s.Title.ToLowerInvariant());
            Bind(insertWork, "$subtitle", s.Subtitle);
            Bind(insertWork, "$authors", JsonArray(s.Authors));
            Bind(insertWork, "$authorSort", s.Authors[0]);
            Bind(insertWork, "$language", s.Language);
            Bind(insertWork, "$year", s.Year);
            Bind(insertWork, "$isbn13", s.Isbn13);
            Bind(insertWork, "$publisher", s.Publisher);
            Bind(insertWork, "$series", s.Series);
            Bind(insertWork, "$seriesIndex", s.SeriesIndex);
            Bind(insertWork, "$edition", s.Edition);
            Bind(insertWork, "$description", s.Description);
            Bind(insertWork, "$rating", s.Rating);
            Bind(insertWork, "$status", s.ReadStatus.ToString().ToLowerInvariant());
            Bind(insertWork, "$cover", s.Cover is null ? null : JoinRelative(dir, s.Cover));
            Bind(insertWork, "$dir", dir);
            Bind(insertWork, "$added", s.Added is { } added ? Timestamp(added.UtcDateTime) : null);
            Bind(insertWork, "$updated", Timestamp(work.SidecarModified));
            var seq = (long)insertWork.ExecuteScalar()!;

            foreach (var tag in s.Tags)
            {
                if (!tagIds.TryGetValue(tag, out var tagId))
                {
                    Bind(insertTag, "$path", tag);
                    insertTag.ExecuteNonQuery();
                    Bind(selectTag, "$path", tag);
                    tagId = (long)selectTag.ExecuteScalar()!;
                    tagIds.Add(tag, tagId);
                }

                Bind(insertWorkTag, "$work", id);
                Bind(insertWorkTag, "$tag", tagId);
                insertWorkTag.ExecuteNonQuery();
            }

            foreach (var file in work.Files)
            {
                Bind(insertFile, "$work", id);
                Bind(insertFile, "$format", FormatOf(file.Entry.Name));
                Bind(insertFile, "$path", file.RelPath);
                Bind(insertFile, "$size", file.Size);
                Bind(insertFile, "$mtime", file.Mtime);
                var fileId = (long)insertFile.ExecuteScalar()!;
                fileCount++;

                if (file.Entry.Progress is { } progress)
                {
                    Bind(insertProgress, "$file", fileId);
                    Bind(insertProgress, "$locator", progress.Locator);
                    Bind(insertProgress, "$updated", Timestamp(progress.UpdatedAt.UtcDateTime));
                    insertProgress.ExecuteNonQuery();
                }
            }

            foreach (var other in s.NotDuplicates.Works)
            {
                InsertIgnore(insertIgnore, id, other.ToString(), "work");
            }

            foreach (var pair in s.NotDuplicates.Files)
            {
                InsertIgnore(insertIgnore, JoinRelative(dir, pair.A), JoinRelative(dir, pair.B), "file");
            }

            Bind(insertFts, "$seq", seq);
            Bind(insertFts, "$title", s.Title);
            Bind(insertFts, "$subtitle", s.Subtitle);
            Bind(insertFts, "$authors", string.Join(' ', s.Authors));
            Bind(insertFts, "$description", s.Description);
            Bind(insertFts, "$tags", string.Join(' ', s.Tags));
            insertFts.ExecuteNonQuery();
        }

        return fileCount;
    }

    private static void InsertIgnore(SqliteCommand command, string x, string y, string kind)
    {
        // The pair is unordered; the CHECK constraint wants a_id < b_id.
        var (a, b) = string.CompareOrdinal(x, y) <= 0 ? (x, y) : (y, x);
        if (a == b)
        {
            return;
        }

        Bind(command, "$a", a);
        Bind(command, "$b", b);
        Bind(command, "$kind", kind);
        command.ExecuteNonQuery();
    }

    private static SqliteCommand Prepare(SqliteConnection connection, string sql)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        return command;
    }

    private static void Bind(SqliteCommand command, string name, object? value)
    {
        if (command.Parameters.Contains(name))
        {
            command.Parameters[name].Value = value ?? DBNull.Value;
        }
        else
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }
    }

    private static string Timestamp(DateTime utc) =>
        utc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static string FormatOf(string fileName) => Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant() switch
    {
        "epub" => "epub",
        "pdf" => "pdf",
        var other when other.Length > 0 => other,
        _ => "other",
    };

    private static string RelativePath(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
        return relative;
    }

    private static string JoinRelative(string directory, string name) => directory == "." ? name : directory + "/" + name;

    private static string JsonArray(IReadOnlyList<string> values)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            writer.WriteStartArray();
            foreach (var value in values)
            {
                writer.WriteStringValue(value);
            }

            writer.WriteEndArray();
        }

        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    private sealed record IndexedFile(SidecarFile Entry, string RelPath, long Size, long Mtime);

    private sealed record IndexedWork(WorkSidecar Sidecar, string DirPath, DateTime SidecarModified, List<IndexedFile> Files);
}
