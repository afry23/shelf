using System.Globalization;
using System.Numerics.Tensors;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Shelf.Core.Model;
using Shelf.Index;

namespace Shelf.Semantic;

/// <summary>A work in a result.</summary>
public sealed record SemanticWork(Ulid Id, string Title, IReadOnlyList<string> Authors, int? Year, string? Language, IReadOnlyList<string> Formats);

/// <summary>Where a passage is: the file, the location in it (<c>page=N</c> or a CFI), for people ("page 2"), and the start of the text.</summary>
public sealed record PassagePlace(string File, string Format, string Locator, string Where, string Snippet);

/// <summary>A hit: a work, or a passage of a work. <paramref name="Score"/> is the cosine similarity (1 same, 0 unrelated).</summary>
public sealed record SemanticHit(SemanticWork Work, double Score, PassagePlace? Passage);

/// <summary>What waits for the embeddings. A result that ignores it can miss works and passages.</summary>
/// <param name="PendingFiles">Files with text that have no vectors (yet or any more).</param>
/// <param name="WorksWithoutVector">Works that have no work vector.</param>
public sealed record SemanticCoverage(int PendingFiles, int WorksWithoutVector)
{
    public bool IsComplete => PendingFiles == 0 && WorksWithoutVector == 0;

    /// <summary>
    /// What to tell the user or the assistant about an incomplete index, or null. <paramref name="noEmbeddings"/> is true when the
    /// index holds no vectors at all.
    /// </summary>
    public string? Note(bool noEmbeddings, string command = "shelf reindex --embeddings")
    {
        if (noEmbeddings && PendingFiles + WorksWithoutVector > 0)
        {
            return $"no embeddings yet; run: {command}";
        }

        if (PendingFiles > 0)
        {
            return PendingFiles == 1
                ? $"1 file still waits for its embeddings; run: {command}"
                : $"{PendingFiles} files still wait for their embeddings; run: {command}";
        }

        return WorksWithoutVector > 0
            ? $"{WorksWithoutVector} {(WorksWithoutVector == 1 ? "work has" : "works have")} no embedding yet; run: {command}"
            : null;
    }
}

/// <summary>Search by meaning (F-SEM-1): the query is embedded as a query and the nearest vectors are looked up.</summary>
public static class SemanticSearch
{
    private const int SnippetLength = 240;

    /// <summary>The works nearest to <paramref name="query"/>, best first.</summary>
    public static List<SemanticHit> SearchWorks(SqliteConnection connection, VectorStore store, ReadOnlySpan<float> query, int limit)
    {
        var near = store.Nearest(VectorKind.Work, query, limit);
        if (near.Count == 0)
        {
            return [];
        }

        var bySeq = new Dictionary<long, SemanticWork>();
        foreach (var work in LoadWorks(connection, "seq", near.Select(h => (object)h.Id)))
        {
            bySeq[work.Seq] = work.Work;
        }

        return [.. near.Where(h => bySeq.ContainsKey(h.Id)).Select(h => new SemanticHit(bySeq[h.Id], Similarity(h.Distance), null))];
    }

    /// <summary>
    /// The works nearest to the work with the ID <paramref name="workId"/> (F-SEM-6), best first, without the work itself; null if
    /// the work has no vector (yet). The vector of the work is used as it is stored: no model is needed.
    /// </summary>
    public static List<SemanticHit>? SimilarWorks(SqliteConnection connection, VectorStore store, string workId, int limit)
    {
        using var find = connection.CreateCommand();
        find.CommandText = "SELECT seq FROM works WHERE id = $id";
        find.Parameters.AddWithValue("$id", workId);
        if (find.ExecuteScalar() is not long seq || store.Get(VectorKind.Work, seq) is not { } vector)
        {
            return null;
        }

        // One more than asked for: the work itself is the nearest vector.
        var near = store.Nearest(VectorKind.Work, vector, limit + 1).Where(h => h.Id != seq).Take(limit).ToList();
        if (near.Count == 0)
        {
            return [];
        }

        var bySeq = LoadWorks(connection, "seq", near.Select(h => (object)h.Id)).ToDictionary(w => w.Seq, w => w.Work);
        return [.. near.Where(h => bySeq.ContainsKey(h.Id)).Select(h => new SemanticHit(bySeq[h.Id], Similarity(h.Distance), null))];
    }

    /// <summary>
    /// Every pair of works whose vectors have a cosine similarity of at least <paramref name="minimum"/> (F-DUP-3). All pairs are
    /// compared in memory, so the cost grows with the square of the number of works.
    /// </summary>
    public static List<WorkSimilarity> SimilarPairs(SqliteConnection connection, VectorStore store, double minimum)
    {
        var vectors = store.All(VectorKind.Work);
        var ids = new Dictionary<long, string>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT seq, id FROM works";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                ids[reader.GetInt64(0)] = reader.GetString(1);
            }
        }

        var known = vectors.Where(v => ids.ContainsKey(v.Id)).ToList();
        foreach (var (_, vector) in known)
        {
            var length = Math.Sqrt(TensorPrimitives.Dot<float>(vector, vector));
            if (length > 0)
            {
                TensorPrimitives.Divide(vector, (float)length, vector);
            }
        }

        var pairs = new List<WorkSimilarity>();
        for (var i = 0; i < known.Count; i++)
        {
            for (var j = i + 1; j < known.Count; j++)
            {
                var cosine = TensorPrimitives.Dot<float>(known[i].Vector, known[j].Vector);
                if (cosine >= minimum)
                {
                    pairs.Add(new WorkSimilarity(Ulid.Parse(ids[known[i].Id]), Ulid.Parse(ids[known[j].Id]), cosine));
                }
            }
        }

        return pairs;
    }

    /// <summary>
    /// What to tell about a search for similar works, or null: the work has no vector, or the index is incomplete.
    /// <paramref name="hits"/> is the result of <see cref="SimilarWorks"/>.
    /// </summary>
    public static string? SimilarNote(List<SemanticHit>? hits, VectorStore? store, SemanticCoverage coverage, string command = "shelf reindex --embeddings")
    {
        var empty = store is null || (store.Count(VectorKind.Work) == 0 && store.Count(VectorKind.Chunk) == 0);
        if (hits is not null)
        {
            return coverage.Note(empty, command);
        }

        return empty
            ? coverage.Note(true, command) ?? $"no embeddings yet; run: {command}"
            : $"this work has no embedding yet; run: {command}";
    }

    /// <summary>
    /// The passages nearest to <paramref name="query"/>, best first, one per chunk (its best window). Passages of files that
    /// wait for their embeddings are left out: their vectors may belong to text that has been replaced.
    /// </summary>
    public static List<SemanticHit> SearchPassages(SqliteConnection connection, VectorStore store, ReadOnlySpan<float> query, int limit)
    {
        // A chunk has up to three windows, so this many windows hold at least `limit` different chunks.
        var near = store.Nearest(VectorKind.Chunk, query, limit * 3);
        var best = new Dictionary<long, (double Distance, int Part)>();
        foreach (var hit in near)
        {
            var chunk = PassageWindows.ChunkOf(hit.Id);
            if (!best.TryGetValue(chunk, out var current) || hit.Distance < current.Distance)
            {
                best[chunk] = (hit.Distance, (int)(hit.Id % PassageWindows.MaxWindows));
            }
        }

        if (best.Count == 0)
        {
            return [];
        }

        var places = LoadPassages(connection, best.Keys);
        return [.. best
            .Where(b => places.ContainsKey(b.Key))
            .OrderBy(b => b.Value.Distance)
            .ThenBy(b => b.Key)
            .Take(limit)
            .Select(b =>
            {
                var (work, file, format, locator, text) = places[b.Key];
                var windows = PassageWindows.Split(text);
                var window = windows[b.Value.Part < windows.Count ? b.Value.Part : 0];
                return new SemanticHit(work, Similarity(b.Value.Distance), new PassagePlace(file, format, locator, Locators.Describe(locator), Shorten(window)));
            })];
    }

    public static SemanticCoverage Coverage(SqliteConnection connection, VectorStore? store)
    {
        using var pending = connection.CreateCommand();
        pending.CommandText = "SELECT count(*) FROM files WHERE text_status = 'done' AND embed_status = 'pending'";
        var files = (int)(long)pending.ExecuteScalar()!;
        using var works = connection.CreateCommand();
        works.CommandText = "SELECT count(*) FROM works";
        var total = (long)works.ExecuteScalar()!;
        var embedded = store?.Count(VectorKind.Work) ?? 0;
        return new SemanticCoverage(files, (int)Math.Max(0, total - embedded));
    }

    private static double Similarity(double distance) => 1.0 - distance;

    private static string Shorten(string text)
    {
        var flat = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (flat.Length <= SnippetLength)
        {
            return flat;
        }

        var cut = flat.LastIndexOf(' ', SnippetLength);
        return flat[..(cut > 0 ? cut : SnippetLength)] + "…";
    }

    private static Dictionary<long, (SemanticWork Work, string File, string Format, string Locator, string Text)> LoadPassages(SqliteConnection connection, IEnumerable<long> chunkIds)
    {
        var ids = chunkIds.ToList();
        var result = new Dictionary<long, (SemanticWork, string, string, string, string)>();
        using var command = connection.CreateCommand();
        command.CommandText =
            $"""
            SELECT c.id, c.text, c.locator, f.rel_path, f.format, w.id
            FROM chunks c JOIN files f ON f.id = c.file_id JOIN works w ON w.id = f.work_id
            WHERE f.embed_status = 'done' AND c.id IN ({Placeholders(command, ids)})
            """;
        var rows = new List<(long Chunk, string Text, string Locator, string File, string Format, string WorkId)>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                rows.Add((reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5)));
            }
        }

        var works = LoadWorks(connection, "id", rows.Select(r => r.WorkId)).ToDictionary(w => w.Work.Id.ToString(), w => w.Work);
        foreach (var row in rows)
        {
            result[row.Chunk] = (works[row.WorkId], row.File, row.Format, row.Locator, row.Text);
        }

        return result;
    }

    private static List<(long Seq, SemanticWork Work)> LoadWorks(SqliteConnection connection, string keyColumn, IEnumerable<object> keys)
    {
        var list = keys.Distinct().ToList();
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT seq, id, title, authors_json, year, language FROM works WHERE {keyColumn} IN ({Placeholders(command, list)})";
        var works = new List<(long, string, string, List<string>, int?, string?)>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                using var authors = JsonDocument.Parse(reader.GetString(3));
                works.Add((
                    reader.GetInt64(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    [.. authors.RootElement.EnumerateArray().Select(a => a.GetString()!)],
                    reader.IsDBNull(4) ? null : reader.GetInt32(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5)));
            }
        }

        var formats = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        using (var command2 = connection.CreateCommand())
        {
            command2.CommandText = $"SELECT DISTINCT work_id, format FROM files WHERE work_id IN ({Placeholders(command2, works.Select(w => (object)w.Item2).ToList())})";
            using var reader = command2.ExecuteReader();
            while (reader.Read())
            {
                if (!formats.TryGetValue(reader.GetString(0), out var set))
                {
                    formats[reader.GetString(0)] = set = new SortedSet<string>(StringComparer.Ordinal);
                }

                set.Add(reader.GetString(1));
            }
        }

        return [.. works.Select(w => (w.Item1, new SemanticWork(Ulid.Parse(w.Item2), w.Item3, w.Item4, w.Item5, w.Item6, [.. formats.GetValueOrDefault(w.Item2) ?? []])))];
    }

    private static string Placeholders<T>(SqliteCommand command, IReadOnlyList<T> values)
    {
        if (values.Count == 0)
        {
            return "NULL";
        }

        var names = new List<string>(values.Count);
        for (var i = 0; i < values.Count; i++)
        {
            var name = "$p" + i.ToString(CultureInfo.InvariantCulture);
            command.Parameters.AddWithValue(name, values[i]);
            names.Add(name);
        }

        return string.Join(", ", names);
    }
}
