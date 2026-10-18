using Microsoft.Data.Sqlite;
using Shelf.Core.Model;
using Shelf.Extraction;

namespace Shelf.Index;

/// <param name="Created">Covers made in this run.</param>
/// <param name="Existing">Works that already had a cover, including a <c>cover.*</c> file that was adopted.</param>
/// <param name="Without">Works that have no cover and no file that gives one.</param>
/// <param name="Problems">Files that could not give a cover; the work stays without one.</param>
public sealed record CoverFillResult(int Created, int Existing, int Without, IReadOnlyList<ReindexProblem> Problems);

/// <summary>
/// Makes the cover images of works (F-IMP-5): <c>cover.&lt;ext&gt;</c> in the folder of the work, named in the sidecar. The first
/// file that has a cover gives it, EPUBs before PDFs (the embedded image is the better picture). A work that has a cover whose file
/// exists is left alone.
/// </summary>
/// <remarks>
/// A cover is derived data: it is written next to the files through a temporary name and a rename, and nothing that existed is
/// replaced, so it is not journaled. A crash between the file and the sidecar leaves a <c>cover.*</c> that the next run adopts.
/// </remarks>
public static class LibraryCovers
{
    private static readonly string[] Extensions = ["jpg", "png", "gif", "webp"];

    /// <summary>Every work of the library.</summary>
    /// <exception cref="IndexException">There is no usable index.</exception>
    public static CoverFillResult FillAll(string root)
    {
        root = Normalize(root);
        using var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(root));
        return Fill(root, Works(connection, "SELECT id, dir_path FROM works ORDER BY dir_path"));
    }

    /// <summary>The works that hold one of these files (library-relative paths), as after a scan that has read them.</summary>
    /// <exception cref="IndexException">There is no usable index.</exception>
    public static CoverFillResult FillFor(string root, IEnumerable<string> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        root = Normalize(root);
        var list = files.ToList();
        if (list.Count == 0)
        {
            // A scan that read nothing opens neither a file nor the index.
            return new CoverFillResult(0, 0, 0, []);
        }

        using var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(root));
        var works = new SortedDictionary<string, string>(StringComparer.Ordinal);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT w.id, w.dir_path FROM files f JOIN works w ON w.id = f.work_id WHERE f.rel_path = $path";
        var path = command.Parameters.Add("$path", SqliteType.Text);
        foreach (var file in list)
        {
            path.Value = file;
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                works[reader.GetString(0)] = reader.GetString(1);
            }
        }

        return Fill(root, [.. works.Select(w => (w.Key, w.Value))]);
    }

    /// <summary>The works with these IDs.</summary>
    public static CoverFillResult FillWorks(string root, IEnumerable<Ulid> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        root = Normalize(root);
        var wanted = ids.Select(i => i.ToString()).ToHashSet(StringComparer.Ordinal);
        using var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(root));
        return Fill(root, [.. Works(connection, "SELECT id, dir_path FROM works ORDER BY dir_path").Where(w => wanted.Contains(w.Id))]);
    }

    private static CoverFillResult Fill(string root, List<(string Id, string Directory)> works)
    {
        int created = 0, existing = 0, without = 0;
        var problems = new List<ReindexProblem>();
        var changed = new List<(string Id, string Directory, string Cover, string SidecarPath)>();
        foreach (var (id, directory) in works)
        {
            var folder = Path.Combine(root, directory);
            var sidecarPath = Path.Combine(folder, SidecarStore.FileName);
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

            if (sidecar.Cover is { } named && File.Exists(Path.Combine(folder, named)))
            {
                existing++;
                continue;
            }

            if (Extensions.Select(e => "cover." + e).FirstOrDefault(n => File.Exists(Path.Combine(folder, n))) is { } adopted)
            {
                SidecarStore.Write(sidecarPath, sidecar with { Cover = adopted });
                changed.Add((id, directory, adopted, sidecarPath));
                existing++;
                continue;
            }

            var cover = Make(folder, sidecar, problems);
            if (cover is null)
            {
                without++;
                continue;
            }

            SidecarStore.Write(sidecarPath, sidecar with { Cover = cover });
            changed.Add((id, directory, cover, sidecarPath));
            created++;
        }

        UpdateIndex(root, changed);
        return new CoverFillResult(created, existing, without, problems);
    }

    /// <summary>
    /// Tells the index about the covers just written: the path of the cover and the time of the sidecar that names it, so that a scan
    /// does not read the sidecar again. Nothing else in the index is touched: a full reconcile would also index works that a rebuild
    /// has just refused.
    /// </summary>
    private static void UpdateIndex(string root, List<(string Id, string Directory, string Cover, string SidecarPath)> changed)
    {
        if (changed.Count == 0)
        {
            return;
        }

        using var connection = IndexDatabase.OpenWrite(IndexDatabase.PathFor(root));
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE works SET cover_path = $cover, updated_at = $stamp WHERE id = $id";
        var cover = command.Parameters.Add("$cover", SqliteType.Text);
        var stamp = command.Parameters.Add("$stamp", SqliteType.Text);
        var id = command.Parameters.Add("$id", SqliteType.Text);
        foreach (var work in changed)
        {
            cover.Value = IndexWriter.JoinRelative(work.Directory, work.Cover);
            stamp.Value = IndexWriter.ModifiedStamp(File.GetLastWriteTimeUtc(work.SidecarPath));
            id.Value = work.Id;
            command.ExecuteNonQuery();
        }

        transaction.Commit();
        IndexDatabase.Checkpoint(connection);
    }

    /// <summary>Writes the cover of the first file that has one and returns its name, or null.</summary>
    private static string? Make(string folder, WorkSidecar sidecar, List<ReindexProblem> problems)
    {
        var ordered = sidecar.Files
            .Select(f => f.Name)
            .OrderBy(n => n.EndsWith(".epub", StringComparison.OrdinalIgnoreCase) ? 0 : 1);
        foreach (var name in ordered)
        {
            var file = Path.Combine(folder, name);
            if (!File.Exists(file))
            {
                continue;
            }

            CoverImage? image;
            try
            {
                image = CoverExtractor.Read(file);
            }
            catch (ExtractionException e)
            {
                problems.Add(new ReindexProblem(file, $"cover: {e.Message}"));
                continue;
            }

            if (image is null)
            {
                continue;
            }

            var coverName = "cover." + image.Extension;
            var target = Path.Combine(folder, coverName);
            var temporary = target + ".tmp";
            try
            {
                File.WriteAllBytes(temporary, image.Content);
                File.Move(temporary, target, overwrite: false);
            }
            catch (IOException e)
            {
                File.Delete(temporary);
                problems.Add(new ReindexProblem(target, $"cover: {e.Message}"));
                continue;
            }

            return coverName;
        }

        return null;
    }

    private static List<(string Id, string Directory)> Works(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        var works = new List<(string, string)>();
        while (reader.Read())
        {
            works.Add((reader.GetString(0), reader.GetString(1)));
        }

        return works;
    }

    private static string Normalize(string root)
    {
        ArgumentException.ThrowIfNullOrEmpty(root);
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
    }
}
