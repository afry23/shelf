using Microsoft.Data.Sqlite;
using Xunit;

namespace Shelf.Index.Tests;

public sealed class IndexDatabaseTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private string NewIndex()
    {
        var path = _temp.Combine("index.db");
        IndexDatabase.Create(path);
        return path;
    }

    private static object? Scalar(SqliteConnection connection, string sql) => IndexDatabase.Scalar(connection, sql);

    [Fact]
    public void A_new_index_has_the_current_schema_version_and_WAL()
    {
        var path = NewIndex();

        using var connection = IndexDatabase.OpenRead(path);

        Assert.Equal((long)IndexSchema.Version, Scalar(connection, "PRAGMA user_version"));
        Assert.Equal("wal", Scalar(connection, "PRAGMA journal_mode"));
    }

    [Fact]
    public void Connections_are_set_up_as_decided_in_ADR_0004()
    {
        using var connection = IndexDatabase.OpenWrite(NewIndex());

        Assert.Equal(1L, Scalar(connection, "PRAGMA synchronous"));
        Assert.Equal(5000L, Scalar(connection, "PRAGMA busy_timeout"));
        Assert.Equal(1L, Scalar(connection, "PRAGMA foreign_keys"));
        Assert.Equal(IndexDatabase.JournalSizeLimitBytes, Scalar(connection, "PRAGMA journal_size_limit"));
    }

    [Fact]
    public void A_reader_cannot_write()
    {
        using var connection = IndexDatabase.OpenRead(NewIndex());

        Assert.Throws<SqliteException>(() => IndexDatabase.Execute(connection, "INSERT INTO tags(path) VALUES ('x')"));
    }

    [Fact]
    public void Create_refuses_an_existing_file()
    {
        var path = NewIndex();

        Assert.Throws<IndexException>(() => IndexDatabase.Create(path));
    }

    [Fact]
    public void Opening_a_missing_index_says_to_run_reindex()
    {
        var error = Assert.Throws<IndexException>(() => IndexDatabase.OpenRead(_temp.Combine("nothing.db")));

        Assert.Contains("run 'shelf reindex'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Opening_an_index_of_another_version_says_to_run_reindex()
    {
        var path = NewIndex();
        using (var connection = IndexDatabase.OpenWrite(path))
        {
            IndexDatabase.Execute(connection, "PRAGMA user_version = 99");
        }

        var error = Assert.Throws<IndexException>(() => IndexDatabase.OpenRead(path));

        Assert.Contains("schema version 99", error.Message, StringComparison.Ordinal);
        Assert.Contains("run 'shelf reindex'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_double_quoted_string_literal_is_an_error()
    {
        // SQLite is built with DQS=0 (ADR 0004): only single quotes make string literals.
        using var connection = IndexDatabase.OpenWrite(NewIndex());

        Assert.Throws<SqliteException>(() => IndexDatabase.Execute(connection, "INSERT INTO tags(path) VALUES (\"x\")"));
    }

    [Fact]
    public void Chunk_text_is_searchable_after_insert_and_gone_after_delete()
    {
        using var connection = IndexDatabase.OpenWrite(NewIndex());
        IndexDatabase.Execute(connection, "INSERT INTO works(id, title, title_norm, authors_json, author_sort, read_status, dir_path, updated_at) VALUES ('W', 't', 't', '[]', 'a', 'unread', '.', 'x')");
        IndexDatabase.Execute(connection, "INSERT INTO files(id, work_id, format, rel_path, size, mtime) VALUES (1, 'W', 'epub', 'a.epub', 1, 1)");
        IndexDatabase.Execute(connection, "INSERT INTO chunks(id, file_id, ordinal, locator, text) VALUES (1, 1, 0, 'page=1', 'the Müller kubernetes cluster')");

        Assert.Equal(1L, Scalar(connection, "SELECT COUNT(*) FROM chunks_fts WHERE chunks_fts MATCH 'muller'"));

        IndexDatabase.Execute(connection, "DELETE FROM files WHERE id = 1");

        Assert.Equal(0L, Scalar(connection, "SELECT COUNT(*) FROM chunks_fts WHERE chunks_fts MATCH 'muller'"));
    }

    [Fact]
    public void Dropping_the_schema_removes_everything_even_with_data_and_foreign_keys()
    {
        using var connection = IndexDatabase.OpenWrite(NewIndex());
        IndexDatabase.Execute(connection, "INSERT INTO works(id, title, title_norm, authors_json, author_sort, read_status, dir_path, updated_at) VALUES ('W', 't', 't', '[]', 'a', 'unread', '.', 'x')");
        IndexDatabase.Execute(connection, "INSERT INTO files(work_id, format, rel_path, size, mtime) VALUES ('W', 'epub', 'a.epub', 1, 1)");

        using var transaction = connection.BeginTransaction();
        IndexDatabase.DropSchema(connection);

        Assert.Equal(0L, Scalar(connection, "SELECT COUNT(*) FROM sqlite_master WHERE name NOT LIKE 'sqlite_%'"));

        IndexDatabase.CreateSchema(connection);
        transaction.Commit();
        Assert.Equal(0L, Scalar(connection, "SELECT COUNT(*) FROM works"));
    }

    [Fact]
    public void Checkpoint_with_a_reader_holding_the_log_does_not_throw()
    {
        var path = NewIndex();
        using var writer = IndexDatabase.OpenWrite(path);
        using var reader = IndexDatabase.OpenRead(path);
        // Deferred: the default transaction of Microsoft.Data.Sqlite is BEGIN IMMEDIATE, which a query_only reader refuses.
        using var readTransaction = reader.BeginTransaction(deferred: true);
        IndexDatabase.Execute(writer, "INSERT INTO tags(path) VALUES ('a')");
        Assert.Equal(1L, Scalar(reader, "SELECT COUNT(*) FROM tags"));

        IndexDatabase.Execute(writer, "INSERT INTO tags(path) VALUES ('b')");

        var exception = Record.Exception(() => IndexDatabase.Checkpoint(writer));
        Assert.Null(exception);
    }

    [Fact]
    public async Task The_log_stays_small_with_a_writer_and_readers_that_pause_between_queries()
    {
        // The realistic pattern from ADR 0004 (interactive use): readers with short pauses (measured: 8 MB).
        var path = NewIndex();
        var stop = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        long maxWal = 0;
        var walPath = path + "-wal";

        Task Reader() => Task.Run(() =>
        {
            using var connection = IndexDatabase.OpenRead(path);
            while (!stop.IsCancellationRequested)
            {
                Scalar(connection, "SELECT COUNT(*) FROM tags");
                Thread.Sleep(20);
            }
        });

        var readers = Enumerable.Range(0, 4).Select(_ => Reader()).ToArray();
        using (var writer = IndexDatabase.OpenWrite(path))
        {
            var i = 0;
            while (!stop.IsCancellationRequested)
            {
                using var transaction = writer.BeginTransaction();
                for (var k = 0; k < 50; k++)
                {
                    IndexDatabase.Execute(writer, $"INSERT INTO tags(path) VALUES ('tag-{i++}')");
                }

                transaction.Commit();
                if (File.Exists(walPath))
                {
                    maxWal = Math.Max(maxWal, new FileInfo(walPath).Length);
                }
            }
        }

        await Task.WhenAll(readers);

        Assert.True(maxWal < 32L * 1024 * 1024, $"WAL grew to {maxWal} bytes");

        // Connections that close at the same moment may each leave the cleanup to the other; the next
        // single connection removes the log when it closes.
        using (IndexDatabase.OpenWrite(path))
        {
        }

        Assert.False(File.Exists(walPath), "the last connection leaves no WAL file");
    }
}
