using Microsoft.Data.Sqlite;
using Shelf.Index;
using Xunit;

namespace Shelf.Semantic.Tests;

public sealed class VectorStoreTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("shelf-vectorstore-").FullName;

    public VectorStoreTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".shelf"));
        IndexDatabase.Create(IndexDatabase.PathFor(_root));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Plain_and_sqlite_vec_find_the_same_neighbours()
    {
        Assert.SkipUnless(File.Exists(SqliteVecExtension.LibraryPath), "vec0 is not next to the test assembly");
        var random = new Random(7);
        var vectors = Enumerable.Range(1, 300).Select(_ => Enumerable.Range(0, 8).Select(_ => (float)(random.NextDouble() - 0.5)).ToArray()).ToList();
        using var plainDb = Open(Path.Combine(_root, "plain.db"));
        using var vecDb = Open(Path.Combine(_root, "vec.db"));
        var plain = VectorStore.Open(plainDb, 8, "m", NoVectorExtension.Instance);
        var vec = VectorStore.Open(vecDb, 8, "m", SqliteVecExtension.Default);
        Assert.Equal(VectorBackend.Plain, plain.Backend);
        Assert.Equal(VectorBackend.SqliteVec, vec.Backend);
        for (var i = 0; i < vectors.Count; i++)
        {
            plain.Put(VectorKind.Chunk, i + 1, vectors[i]);
            vec.Put(VectorKind.Chunk, i + 1, vectors[i]);
        }

        foreach (var query in vectors.Take(5))
        {
            var a = plain.Nearest(VectorKind.Chunk, query, 10);
            var b = vec.Nearest(VectorKind.Chunk, query, 10);
            Assert.Equal(b.Select(h => h.Id), a.Select(h => h.Id));
            Assert.All(a.Zip(b), p => Assert.Equal(p.Second.Distance, p.First.Distance, 4));
        }
    }

    [Fact]
    public void Vectors_of_another_model_are_refused_until_reset()
    {
        using var db = IndexDatabase.OpenWrite(IndexDatabase.PathFor(_root));
        VectorStore.Open(db, 3, "old", NoVectorExtension.Instance).Put(VectorKind.Work, 1, [1f, 0f, 0f]);

        var store = VectorStore.Open(db, 3, "new", NoVectorExtension.Instance);

        Assert.False(store.IsCurrentModel);
        Assert.Throws<VectorStoreException>(() => store.Put(VectorKind.Work, 2, [0f, 1f, 0f]));
        var fresh = store.Reset(NoVectorExtension.Instance);
        Assert.True(fresh.IsCurrentModel);
        Assert.Equal(0, fresh.Count(VectorKind.Work));
        fresh.Put(VectorKind.Work, 2, [0f, 1f, 0f]);
        Assert.Equal(1, fresh.Count(VectorKind.Work));
    }

    [Fact]
    public void A_different_dimension_counts_as_another_model()
    {
        using var db = IndexDatabase.OpenWrite(IndexDatabase.PathFor(_root));
        VectorStore.Open(db, 3, "m", NoVectorExtension.Instance);

        Assert.False(VectorStore.Open(db, 4, "m", NoVectorExtension.Instance).IsCurrentModel);
    }

    [Fact]
    public void An_index_without_vectors_has_nothing_to_open_for_reading()
    {
        using var db = IndexDatabase.OpenRead(IndexDatabase.PathFor(_root));

        Assert.Null(VectorStore.OpenExisting(db));
    }

    [Fact]
    public void An_existing_store_is_read_with_its_own_model_on_a_query_only_connection()
    {
        using (var write = IndexDatabase.OpenWrite(IndexDatabase.PathFor(_root)))
        {
            VectorStore.Open(write, 3, "stored-model", NoVectorExtension.Instance).Put(VectorKind.Work, 5, [0f, 1f, 0f]);
        }

        using var read = IndexDatabase.OpenRead(IndexDatabase.PathFor(_root));
        var store = Assert.IsType<VectorStore>(VectorStore.OpenExisting(read, NoVectorExtension.Instance));

        Assert.Equal("stored-model", store.Model);
        Assert.Equal(3, store.Dimensions);
        Assert.True(store.IsCurrentModel);
        Assert.Equal(5, Assert.Single(store.Nearest(VectorKind.Work, [0f, 1f, 0f], 1)).Id);
    }

    [Fact]
    public void The_codec_round_trips_little_endian_floats()
    {
        float[] values = [1.5f, -2f, 0f, float.Epsilon];

        var bytes = VectorCodec.ToBytes(values);
        var back = new float[4];
        VectorCodec.CopyTo(bytes, back);

        Assert.Equal(16, bytes.Length);
        Assert.Equal([0, 0, 0xC0, 0x3F], bytes[..4]);
        Assert.Equal(values, back);
    }

    private static SqliteConnection Open(string path)
    {
        IndexDatabase.Create(path);
        return IndexDatabase.OpenWrite(path);
    }
}
