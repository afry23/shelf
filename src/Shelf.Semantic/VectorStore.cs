using System.Globalization;
using System.Numerics.Tensors;
using System.Runtime.InteropServices;
using Microsoft.Data.Sqlite;
using Shelf.Index;

namespace Shelf.Semantic;

public enum VectorKind
{
    Work,
    Chunk,
}

public enum VectorBackend
{
    /// <summary>Virtual tables of the sqlite-vec extension.</summary>
    SqliteVec,

    /// <summary>Plain tables with the vectors as BLOBs, searched by a scan in C#.</summary>
    Plain,
}

/// <summary>A found vector: the id it was stored under and its cosine distance to the query (0 same, 1 unrelated, 2 opposite).</summary>
public sealed record VectorHit(long Id, double Distance);

public sealed class VectorStoreException(string message) : Exception(message);

/// <summary>
/// The vectors of works (<c>vec_works</c>, keyed by <c>works.seq</c>) and of passages (<c>vec_chunks</c>, keyed by
/// <c>chunks.id</c>) inside <c>index.db</c> (F-SEM-4). The first open decides the backend: sqlite-vec if its library
/// loads, else plain tables; the choice is stored in <c>semantic_meta</c> together with the model and the dimension, so
/// the same index is always read the same way. A plain index is searched by a scan, which costs time on big libraries
/// (ADR 0004); <see cref="Reset"/> starts over with the best backend that is available.
/// </summary>
public sealed class VectorStore
{
    private const string BackendKey = "backend";
    private const string ModelKey = "model";
    private const string DimensionsKey = "dimensions";

    private readonly SqliteConnection _connection;

    private VectorStore(SqliteConnection connection, int dimensions, string model, VectorBackend backend, string? storedModel, int? storedDimensions)
    {
        _connection = connection;
        Dimensions = dimensions;
        Model = model;
        Backend = backend;
        StoredModel = storedModel;
        StoredDimensions = storedDimensions;
    }

    public int Dimensions { get; }

    public string Model { get; }

    public VectorBackend Backend { get; }

    /// <summary>The model that made the vectors in the index, or null for a new index.</summary>
    public string? StoredModel { get; }

    public int? StoredDimensions { get; }

    /// <summary>False when the index holds vectors of another model or dimension: <see cref="Put"/> refuses until <see cref="Reset"/>.</summary>
    public bool IsCurrentModel => StoredModel is null || (StoredModel == Model && StoredDimensions == Dimensions);

    /// <summary>
    /// Opens the vector tables of an open index connection, creating them in a new index.
    /// </summary>
    /// <exception cref="VectorStoreException">The index uses sqlite-vec and its library cannot be loaded.</exception>
    public static VectorStore Open(SqliteConnection connection, int dimensions, string model, IVectorExtension? extension = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentOutOfRangeException.ThrowIfLessThan(dimensions, 1);
        ArgumentException.ThrowIfNullOrEmpty(model);
        extension ??= SqliteVecExtension.Default;

        Execute(connection, $"CREATE TABLE IF NOT EXISTS {VectorTables.Meta}(key TEXT PRIMARY KEY, value TEXT NOT NULL)");
        var meta = ReadMeta(connection);
        if (meta.ContainsKey(BackendKey))
        {
            return FromMeta(connection, meta, dimensions, model, extension);
        }

        var chosen = extension.TryLoad(connection) ? VectorBackend.SqliteVec : VectorBackend.Plain;
        CreateTables(connection, chosen, dimensions);
        WriteMeta(connection, chosen, model, dimensions);
        return new VectorStore(connection, dimensions, model, chosen, model, dimensions);
    }

    /// <summary>
    /// Opens the vector tables for reading, as they are: the model and dimension are those stored in the index. Null when
    /// the index holds no vectors yet. Works on a <c>query_only</c> connection; nothing is created.
    /// </summary>
    /// <exception cref="VectorStoreException">The index uses sqlite-vec and its library cannot be loaded.</exception>
    public static VectorStore? OpenExisting(SqliteConnection connection, IVectorExtension? extension = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        using var exists = connection.CreateCommand();
        exists.CommandText = $"SELECT count(*) FROM sqlite_master WHERE name = '{VectorTables.Meta}'";
        if ((long)exists.ExecuteScalar()! == 0)
        {
            return null;
        }

        var meta = ReadMeta(connection);
        if (!meta.TryGetValue(BackendKey, out _) || !meta.TryGetValue(ModelKey, out var model) || !meta.TryGetValue(DimensionsKey, out var dimensions))
        {
            return null;
        }

        return FromMeta(connection, meta, int.Parse(dimensions, CultureInfo.InvariantCulture), model, extension ?? SqliteVecExtension.Default);
    }

    private static VectorStore FromMeta(SqliteConnection connection, Dictionary<string, string> meta, int dimensions, string model, IVectorExtension extension)
    {
        var backend = meta[BackendKey] == "sqlite-vec" ? VectorBackend.SqliteVec : VectorBackend.Plain;
        if (backend == VectorBackend.SqliteVec && !extension.TryLoad(connection))
        {
            throw new VectorStoreException(
                "this index keeps its vectors with sqlite-vec, but the library cannot be loaded; "
                + "restore vec0 next to the executable or run: shelf reindex");
        }

        return new VectorStore(
            connection,
            dimensions,
            model,
            backend,
            meta.GetValueOrDefault(ModelKey),
            meta.TryGetValue(DimensionsKey, out var d) ? int.Parse(d, CultureInfo.InvariantCulture) : null);
    }

    /// <summary>Removes every vector and starts over for the current model, with the best backend that is available.</summary>
    public VectorStore Reset(IVectorExtension? extension = null)
    {
        extension ??= SqliteVecExtension.Default;
        VectorTables.Drop(_connection);
        Execute(_connection, $"CREATE TABLE {VectorTables.Meta}(key TEXT PRIMARY KEY, value TEXT NOT NULL)");
        var chosen = extension.TryLoad(_connection) ? VectorBackend.SqliteVec : VectorBackend.Plain;
        CreateTables(_connection, chosen, Dimensions);
        WriteMeta(_connection, chosen, Model, Dimensions);
        return new VectorStore(_connection, Dimensions, Model, chosen, Model, Dimensions);
    }

    /// <summary>Stores the vector under <paramref name="id"/>, replacing an earlier one.</summary>
    public void Put(VectorKind kind, long id, ReadOnlySpan<float> vector)
    {
        CheckDimensions(vector.Length);
        if (!IsCurrentModel)
        {
            throw new VectorStoreException($"the vectors in this index were made with {StoredModel} ({StoredDimensions} dimensions), not {Model}");
        }

        var table = TableOf(kind);
        var blob = VectorCodec.ToBytes(vector);
        using var transaction = _connection.BeginTransaction();
        Delete(kind, id);
        using var command = _connection.CreateCommand();
        command.CommandText = $"INSERT INTO {table}({IdColumn(kind)}, embedding) VALUES ($id, $e)";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$e", blob);
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    public void Delete(VectorKind kind, long id)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = $"DELETE FROM {TableOf(kind)} WHERE {IdColumn(kind)} = $id";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    /// <summary>Every id that has a vector.</summary>
    public IReadOnlyList<long> Ids(VectorKind kind)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = $"SELECT {IdColumn(kind)} FROM {TableOf(kind)}";
        using var reader = command.ExecuteReader();
        var ids = new List<long>();
        while (reader.Read())
        {
            ids.Add(reader.GetInt64(0));
        }

        return ids;
    }

    public long Count(VectorKind kind)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = $"SELECT count(*) FROM {TableOf(kind)}";
        return (long)command.ExecuteScalar()!;
    }

    /// <summary>The <paramref name="k"/> vectors nearest to <paramref name="query"/> by cosine distance, nearest first.</summary>
    public IReadOnlyList<VectorHit> Nearest(VectorKind kind, ReadOnlySpan<float> query, int k)
    {
        CheckDimensions(query.Length);
        ArgumentOutOfRangeException.ThrowIfLessThan(k, 1);
        return Backend == VectorBackend.SqliteVec ? NearestWithExtension(kind, query, k) : NearestByScan(kind, query, k);
    }

    private List<VectorHit> NearestWithExtension(VectorKind kind, ReadOnlySpan<float> query, int k)
    {
        using var command = _connection.CreateCommand();
        command.CommandText =
            $"SELECT {IdColumn(kind)}, distance FROM {TableOf(kind)} WHERE embedding MATCH $q AND k = {k.ToString(CultureInfo.InvariantCulture)} ORDER BY distance";
        command.Parameters.AddWithValue("$q", VectorCodec.ToBytes(query));
        using var reader = command.ExecuteReader();
        var hits = new List<VectorHit>();
        while (reader.Read())
        {
            hits.Add(new VectorHit(reader.GetInt64(0), reader.GetDouble(1)));
        }

        return hits;
    }

    private List<VectorHit> NearestByScan(VectorKind kind, ReadOnlySpan<float> query, int k)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = $"SELECT {IdColumn(kind)}, embedding FROM {TableOf(kind)}";
        using var reader = command.ExecuteReader();
        var best = new PriorityQueue<long, double>(); // max-heap on distance through the negated priority
        var distances = new Dictionary<long, double>();
        var candidate = new float[Dimensions];
        while (reader.Read())
        {
            VectorCodec.CopyTo(reader.GetFieldValue<byte[]>(1), candidate);
            var distance = 1.0 - TensorPrimitives.CosineSimilarity(query, candidate);
            var id = reader.GetInt64(0);
            if (best.Count < k)
            {
                best.Enqueue(id, -distance);
                distances[id] = distance;
            }
            else if (distance < -PeekPriority(best))
            {
                var dropped = best.Dequeue();
                distances.Remove(dropped);
                best.Enqueue(id, -distance);
                distances[id] = distance;
            }
        }

        return [.. distances.Select(d => new VectorHit(d.Key, d.Value)).OrderBy(h => h.Distance).ThenBy(h => h.Id)];
    }

    private static double PeekPriority(PriorityQueue<long, double> queue)
    {
        queue.TryPeek(out _, out var priority);
        return priority;
    }

    private void CheckDimensions(int length)
    {
        if (length != Dimensions)
        {
            throw new ArgumentException($"expected {Dimensions} dimensions but got {length}", "vector");
        }
    }

    private static string TableOf(VectorKind kind) => kind == VectorKind.Work ? VectorTables.Works : VectorTables.Chunks;

    private static string IdColumn(VectorKind kind) => kind == VectorKind.Work ? "work_seq" : "chunk_id";

    private static void CreateTables(SqliteConnection connection, VectorBackend backend, int dimensions)
    {
        foreach (var kind in new[] { VectorKind.Work, VectorKind.Chunk })
        {
            Execute(
                connection,
                backend == VectorBackend.SqliteVec
                    ? $"CREATE VIRTUAL TABLE {TableOf(kind)} USING vec0({IdColumn(kind)} INTEGER PRIMARY KEY, embedding float[{dimensions.ToString(CultureInfo.InvariantCulture)}] distance_metric=cosine)"
                    : $"CREATE TABLE {TableOf(kind)}({IdColumn(kind)} INTEGER PRIMARY KEY, embedding BLOB NOT NULL)");
        }
    }

    private static void WriteMeta(SqliteConnection connection, VectorBackend backend, string model, int dimensions)
    {
        foreach (var (key, value) in new[]
        {
            (BackendKey, backend == VectorBackend.SqliteVec ? "sqlite-vec" : "plain"),
            (ModelKey, model),
            (DimensionsKey, dimensions.ToString(CultureInfo.InvariantCulture)),
        })
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"INSERT OR REPLACE INTO {VectorTables.Meta}(key, value) VALUES ($k, $v)";
            command.Parameters.AddWithValue("$k", key);
            command.Parameters.AddWithValue("$v", value);
            command.ExecuteNonQuery();
        }
    }

    private static Dictionary<string, string> ReadMeta(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT key, value FROM {VectorTables.Meta}";
        using var reader = command.ExecuteReader();
        var meta = new Dictionary<string, string>();
        while (reader.Read())
        {
            meta[reader.GetString(0)] = reader.GetString(1);
        }

        return meta;
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}

/// <summary>Vectors as little-endian float32 BLOBs, the format of sqlite-vec.</summary>
internal static class VectorCodec
{
    public static byte[] ToBytes(ReadOnlySpan<float> vector)
    {
        var bytes = new byte[vector.Length * sizeof(float)];
        MemoryMarshal.AsBytes(vector).CopyTo(bytes);
        if (!BitConverter.IsLittleEndian)
        {
            for (var i = 0; i < bytes.Length; i += sizeof(float))
            {
                Array.Reverse(bytes, i, sizeof(float));
            }
        }

        return bytes;
    }

    public static void CopyTo(byte[] bytes, float[] destination)
    {
        if (bytes.Length != destination.Length * sizeof(float))
        {
            throw new VectorStoreException($"a stored vector has {bytes.Length / sizeof(float)} dimensions, expected {destination.Length}");
        }

        for (var i = 0; i < destination.Length; i++)
        {
            destination[i] = BitConverter.ToSingle(bytes, i * sizeof(float));
        }
    }
}
