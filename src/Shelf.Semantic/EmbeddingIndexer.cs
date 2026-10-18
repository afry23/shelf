using Microsoft.Data.Sqlite;
using Shelf.Index;

namespace Shelf.Semantic;

/// <param name="Phase"><c>works</c> or <c>files</c>.</param>
public sealed record EmbeddingProgress(string Phase, int Done, int Total);

/// <param name="Works">Work vectors made in this run.</param>
/// <param name="Passages">Passage vectors (windows) made in this run, also those of files that did not finish.</param>
/// <param name="Files">Files finished in this run.</param>
/// <param name="Remaining">Files with text that still wait for their vectors.</param>
public sealed record EmbeddingResult(int Works, int Passages, int Files, int Remaining, bool Interrupted, IReadOnlyList<string> Problems);

/// <summary>
/// Makes the vectors of an index (F-SEM-3, F-SEM-2, F-SEM-7): first one per work, then the windows of every passage of
/// every file whose <c>embed_status</c> is <c>pending</c>. A file is marked <c>done</c> when all its windows are stored,
/// so a stopped run loses at most the file it was working on, and the next run starts with the files that are left.
/// </summary>
public static class EmbeddingIndexer
{
    /// <exception cref="VectorStoreException">The index holds vectors of another model.</exception>
    public static EmbeddingResult Run(
        SqliteConnection connection,
        IEmbedder embedder,
        VectorStore store,
        Action<EmbeddingProgress>? progress = null,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(embedder);
        ArgumentNullException.ThrowIfNull(store);
        if (!store.IsCurrentModel)
        {
            throw new VectorStoreException($"the vectors in this index were made with {store.StoredModel} ({store.StoredDimensions} dimensions), not {store.Model}");
        }

        Execute(connection, $"CREATE TABLE IF NOT EXISTS {VectorTables.WorkHashes}(work_seq INTEGER PRIMARY KEY, hash TEXT NOT NULL)");
        PurgeOrphans(connection, store);

        var problems = new List<string>();
        var works = EmbedWorks(connection, embedder, store, progress, cancellation);
        if (cancellation.IsCancellationRequested)
        {
            return new EmbeddingResult(works, 0, 0, CountPending(connection), Interrupted: true, problems);
        }

        var pending = PendingFiles(connection);
        var passages = 0;
        var files = 0;
        foreach (var file in pending)
        {
            if (cancellation.IsCancellationRequested)
            {
                break;
            }

            try
            {
                var (windows, finished) = EmbedFile(connection, embedder, store, file, cancellation);
                passages += windows;
                if (finished)
                {
                    files++;
                    progress?.Invoke(new EmbeddingProgress("files", files, pending.Count));
                }
            }
            catch (Exception e) when (e is not (OperationCanceledException or SqliteException))
            {
                problems.Add($"{file.RelPath}: {e.Message}");
                Execute(connection, "UPDATE files SET embed_status = 'failed' WHERE id = $id", ("$id", file.Id));
            }
        }

        return new EmbeddingResult(works, passages, files, CountPending(connection), cancellation.IsCancellationRequested, problems);
    }

    /// <summary>Removes vectors whose work or passage is gone, and the hashes of gone works.</summary>
    private static void PurgeOrphans(SqliteConnection connection, VectorStore store)
    {
        var works = new HashSet<long>(Longs(connection, "SELECT seq FROM works"));
        foreach (var id in store.Ids(VectorKind.Work).Where(id => !works.Contains(id)))
        {
            store.Delete(VectorKind.Work, id);
        }

        Execute(connection, $"DELETE FROM {VectorTables.WorkHashes} WHERE work_seq NOT IN (SELECT seq FROM works)");

        var chunks = new HashSet<long>(Longs(connection, "SELECT id FROM chunks"));
        foreach (var key in store.Ids(VectorKind.Chunk).Where(key => !chunks.Contains(PassageWindows.ChunkOf(key))))
        {
            store.Delete(VectorKind.Chunk, key);
        }
    }

    private static int EmbedWorks(SqliteConnection connection, IEmbedder embedder, VectorStore store, Action<EmbeddingProgress>? progress, CancellationToken cancellation)
    {
        var tags = new Dictionary<string, List<string>>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT wt.work_id, t.path FROM work_tags wt JOIN tags t ON t.id = wt.tag_id ORDER BY wt.work_id, t.path";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (!tags.TryGetValue(reader.GetString(0), out var list))
                {
                    tags[reader.GetString(0)] = list = [];
                }

                list.Add(reader.GetString(1));
            }
        }

        var hashes = new Dictionary<long, string>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = $"SELECT work_seq, hash FROM {VectorTables.WorkHashes}";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                hashes[reader.GetInt64(0)] = reader.GetString(1);
            }
        }

        var rows = new List<(long Seq, string Text, string Hash)>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT seq, id, title, subtitle, description FROM works ORDER BY seq";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var text = WorkText.Build(
                    reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    tags.GetValueOrDefault(reader.GetString(1)) ?? []);
                rows.Add((reader.GetInt64(0), text, WorkText.Hash(text)));
            }
        }

        var changed = rows.Where(r => !hashes.TryGetValue(r.Seq, out var old) || old != r.Hash).ToList();
        var done = 0;
        foreach (var (seq, text, hash) in changed)
        {
            if (cancellation.IsCancellationRequested)
            {
                break;
            }

            store.Put(VectorKind.Work, seq, embedder.EmbedPassage(text));
            Execute(connection, $"INSERT OR REPLACE INTO {VectorTables.WorkHashes}(work_seq, hash) VALUES ($s, $h)", ("$s", seq), ("$h", hash));
            progress?.Invoke(new EmbeddingProgress("works", ++done, changed.Count));
        }

        return done;
    }

    private sealed record PendingFile(long Id, string RelPath, string? Sha256, long Size, long Mtime);

    private static List<PendingFile> PendingFiles(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, rel_path, sha256, size, mtime FROM files WHERE text_status = 'done' AND embed_status = 'pending' ORDER BY id";
        using var reader = command.ExecuteReader();
        var files = new List<PendingFile>();
        while (reader.Read())
        {
            files.Add(new PendingFile(reader.GetInt64(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.GetInt64(3), reader.GetInt64(4)));
        }

        return files;
    }

    /// <summary>The file as it is now, or null when it is gone or no longer waits.</summary>
    private static PendingFile? Current(SqliteConnection connection, long id)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, rel_path, sha256, size, mtime FROM files WHERE id = $id AND text_status = 'done' AND embed_status = 'pending'";
        command.Parameters.AddWithValue("$id", id);
        using var reader = command.ExecuteReader();
        return reader.Read()
            ? new PendingFile(reader.GetInt64(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.GetInt64(3), reader.GetInt64(4))
            : null;
    }

    private static (int Windows, bool Finished) EmbedFile(SqliteConnection connection, IEmbedder embedder, VectorStore store, PendingFile file, CancellationToken cancellation)
    {
        var chunks = new List<(long Id, string Text)>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT id, text FROM chunks WHERE file_id = $f ORDER BY ordinal";
            command.Parameters.AddWithValue("$f", file.Id);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                chunks.Add((reader.GetInt64(0), reader.GetString(1)));
            }
        }

        var windows = 0;
        foreach (var (id, text) in chunks)
        {
            var parts = PassageWindows.Split(text);
            for (var part = 0; part < parts.Count; part++)
            {
                if (cancellation.IsCancellationRequested)
                {
                    return (windows, false);
                }

                store.Put(VectorKind.Chunk, PassageWindows.KeyOf(id, part), embedder.EmbedPassage(parts[part]));
                windows++;
            }
        }

        // A scan that replaced the file while its windows were made has put new chunks under the same file: leave it pending.
        var after = Current(connection, file.Id);
        if (after is null || after.Sha256 != file.Sha256 || after.Size != file.Size || after.Mtime != file.Mtime)
        {
            return (windows, false);
        }

        Execute(connection, "UPDATE files SET embed_status = 'done' WHERE id = $id", ("$id", file.Id));
        return (windows, true);
    }

    private static int CountPending(SqliteConnection connection) =>
        (int)(long)Scalar(connection, "SELECT count(*) FROM files WHERE text_status = 'done' AND embed_status = 'pending'")!;

    private static IEnumerable<long> Longs(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        var values = new List<long>();
        while (reader.Read())
        {
            values.Add(reader.GetInt64(0));
        }

        return values;
    }

    private static object? Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    private static void Execute(SqliteConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        command.ExecuteNonQuery();
    }
}
