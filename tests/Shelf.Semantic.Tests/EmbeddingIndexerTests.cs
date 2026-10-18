using Microsoft.Data.Sqlite;
using Shelf.Index;
using Xunit;

namespace Shelf.Semantic.Tests;

public sealed class EmbeddingIndexerTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("shelf-indexer-").FullName;
    private readonly SqliteConnection _db;

    public EmbeddingIndexerTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".shelf"));
        IndexDatabase.Create(IndexDatabase.PathFor(_root));
        _db = IndexDatabase.OpenWrite(IndexDatabase.PathFor(_root));
    }

    public void Dispose()
    {
        _db.Dispose();
        SqliteConnection.ClearAllPools();
        Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void A_file_that_changes_while_it_is_embedded_stays_pending()
    {
        Seed(workSeq: 1, fileId: 1, chunks: 2);
        var embedder = new Fake(() => Sql("UPDATE files SET sha256 = 'changed' WHERE id = 1"), onlyForPassages: true);

        var result = Run(embedder, TestContext.Current.CancellationToken);

        Assert.Equal(0, result.Files);
        Assert.Equal(1, result.Remaining);
        Assert.Equal("pending", Status(1));
    }

    [Fact]
    public void A_file_whose_embedding_fails_is_marked_and_the_others_go_on()
    {
        Seed(workSeq: 1, fileId: 1, chunks: 1);
        Seed(workSeq: 2, fileId: 2, chunks: 1);
        var embedder = new Fake(onPassage: text => text == "text 1 of 1" && FailOnce(text));

        var result = Run(embedder, TestContext.Current.CancellationToken);

        Assert.Equal(1, result.Files);
        Assert.Equal(0, result.Remaining);
        Assert.Single(result.Problems);
        Assert.StartsWith("a/1.epub: boom", result.Problems[0]);
        Assert.Equal("failed", Status(1));
        Assert.Equal("done", Status(2));
    }

    [Fact]
    public void Interrupting_between_two_windows_leaves_the_file_pending_and_the_next_run_finishes_it()
    {
        Seed(workSeq: 1, fileId: 1, chunks: 3);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var calls = 0;
        var embedder = new Fake(afterCall: () =>
        {
            if (++calls == 3)
            {
                stop.Cancel();
            }
        });

        var first = Run(embedder, stop.Token);
        var second = Run(new Fake(), TestContext.Current.CancellationToken);

        Assert.True(first.Interrupted);
        Assert.Equal(1, first.Remaining);
        Assert.False(second.Interrupted);
        Assert.Equal(0, second.Remaining);
        Assert.Equal(3, second.Passages);
        Assert.Equal(0, second.Works);
        Assert.Equal("done", Status(1));
    }

    [Fact]
    public void Vectors_of_removed_chunks_and_works_are_purged_at_the_start()
    {
        Seed(workSeq: 1, fileId: 1, chunks: 2);
        Run(new Fake(), TestContext.Current.CancellationToken);
        Sql("DELETE FROM chunks WHERE file_id = 1 AND ordinal = 1");
        Sql("DELETE FROM files WHERE id = 1");
        Sql("DELETE FROM works WHERE seq = 1");

        Run(new Fake(), TestContext.Current.CancellationToken);
        var store = VectorStore.Open(_db, 3, "m", NoVectorExtension.Instance);

        Assert.Equal(0, store.Count(VectorKind.Work));
        Assert.Equal(0, store.Count(VectorKind.Chunk));
        Assert.Equal(0L, Scalar($"SELECT count(*) FROM {VectorTables.WorkHashes}"));
    }

    [Fact]
    public void An_index_of_another_model_is_refused()
    {
        VectorStore.Open(_db, 3, "old", NoVectorExtension.Instance);
        var store = VectorStore.Open(_db, 3, "new", NoVectorExtension.Instance);

        Assert.Throws<VectorStoreException>(() => EmbeddingIndexer.Run(_db, new Fake(), store, cancellation: TestContext.Current.CancellationToken));
    }

    private readonly HashSet<string> _failed = [];

    private bool FailOnce(string text) => _failed.Add(text);

    private EmbeddingResult Run(Fake embedder, CancellationToken cancellation) =>
        EmbeddingIndexer.Run(_db, embedder, VectorStore.Open(_db, 3, "m", NoVectorExtension.Instance), cancellation: cancellation);

    private void Seed(int workSeq, int fileId, int chunks)
    {
        Sql(
            $"INSERT INTO works(seq, id, title, title_norm, authors_json, author_sort, read_status, dir_path, updated_at) "
            + $"VALUES ({workSeq}, 'w{workSeq}', 'Title {workSeq}', 'title {workSeq}', '[]', 'a', 'unread', 'a', '2026-01-01')");
        Sql(
            $"INSERT INTO files(id, work_id, format, rel_path, size, sha256, mtime, text_status) "
            + $"VALUES ({fileId}, 'w{workSeq}', 'epub', 'a/{fileId}.epub', 10, 'x', 1, 'done')");
        for (var i = 0; i < chunks; i++)
        {
            Sql($"INSERT INTO chunks(file_id, ordinal, locator, text) VALUES ({fileId}, {i}, 'l', 'text {i + 1} of {chunks}')");
        }
    }

    private void Sql(string sql)
    {
        using var command = _db.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private object? Scalar(string sql)
    {
        using var command = _db.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    private string Status(int fileId) => (string)Scalar($"SELECT embed_status FROM files WHERE id = {fileId}")!;

    private sealed class Fake(Action? afterCall = null, bool onlyForPassages = false, Func<string, bool>? onPassage = null) : IEmbedder
    {
        public int Dimensions => 3;

        public float[] EmbedQuery(string text) => [1f, 0f, 0f];

        public float[] EmbedPassage(string text)
        {
            if (onPassage?.Invoke(text) == true)
            {
                throw new InvalidOperationException("boom");
            }

            if (!onlyForPassages || !text.StartsWith("Title", StringComparison.Ordinal))
            {
                afterCall?.Invoke();
            }

            return [0f, 1f, 0f];
        }
    }
}
