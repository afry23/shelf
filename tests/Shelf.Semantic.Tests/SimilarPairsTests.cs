using Microsoft.Data.Sqlite;
using Shelf.Core.Model;
using Shelf.Index;
using Xunit;

namespace Shelf.Semantic.Tests;

public sealed class SimilarPairsTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("shelf-similarpairs-").FullName;

    public SimilarPairsTests()
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
    public void Pairs_from_the_minimum_on_are_returned_with_their_cosine_whatever_the_length_of_the_vectors()
    {
        using var db = IndexDatabase.OpenWrite(IndexDatabase.PathFor(_root));
        var ids = new[] { Ulid.New(), Ulid.New(), Ulid.New() };
        for (var i = 0; i < ids.Length; i++)
        {
            using var insert = db.CreateCommand();
            insert.CommandText = "INSERT INTO works(seq, id, title, title_norm, authors_json, author_sort, read_status, dir_path, updated_at) VALUES ($seq, $id, 't', 't', '[]', '', 'unread', '.', 'now')";
            insert.Parameters.AddWithValue("$seq", i + 1);
            insert.Parameters.AddWithValue("$id", ids[i].ToString());
            insert.ExecuteNonQuery();
        }

        var store = VectorStore.Open(db, 2, "m", NoVectorExtension.Instance);
        store.Put(VectorKind.Work, 1, [1f, 0f]);
        store.Put(VectorKind.Work, 2, [3f, 0.3f]); // 5.7 degrees from the first, much longer
        store.Put(VectorKind.Work, 3, [0f, 1f]);

        var pairs = SemanticSearch.SimilarPairs(db, store, 0.9);

        var pair = Assert.Single(pairs);
        Assert.Equal((ids[0], ids[1]), (pair.A, pair.B));
        Assert.Equal(0.995, pair.Cosine, 3);
        Assert.Equal(3, SemanticSearch.SimilarPairs(db, store, -1).Count);
    }

    [Fact]
    public void A_vector_of_a_work_that_is_gone_is_left_out()
    {
        using var db = IndexDatabase.OpenWrite(IndexDatabase.PathFor(_root));
        var store = VectorStore.Open(db, 2, "m", NoVectorExtension.Instance);
        store.Put(VectorKind.Work, 1, [1f, 0f]);
        store.Put(VectorKind.Work, 2, [1f, 0f]);

        Assert.Empty(SemanticSearch.SimilarPairs(db, store, 0.5));
    }
}
