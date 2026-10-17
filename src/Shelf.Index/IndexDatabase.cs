using Microsoft.Data.Sqlite;

namespace Shelf.Index;

/// <summary>The index database is missing, unreadable or has another schema version.</summary>
public sealed class IndexException(string message, Exception? innerException = null) : Exception(message, innerException);

/// <summary>
/// Opens <c>.shelf/index.db</c> with the connection setup from ADR 0004: WAL, <c>synchronous=NORMAL</c>,
/// 5 s busy timeout, foreign keys on. CLI, web and MCP processes use the same file at the same time (PRD 9.3).
/// </summary>
/// <remarks>
/// WAL policy (ADR 0004 hand-off): <c>journal_size_limit</c> caps the WAL file left after a checkpoint;
/// bulk writers call <see cref="Checkpoint"/> when they are done, so the log does not stay large. Pooling is off:
/// the last closed connection then checkpoints and removes the WAL file. A reader that queries without any
/// pause can keep the WAL growing (measured: 1 GB in 30 s); interactive use does not (8 MB with 20 ms pauses).
/// </remarks>
public static class IndexDatabase
{
    public const string FileName = "index.db";
    public const long JournalSizeLimitBytes = 64L * 1024 * 1024;
    public const int BusyTimeoutMilliseconds = 5000;

    /// <summary>The path of the index of a library.</summary>
    public static string PathFor(string libraryRoot) => Path.Combine(libraryRoot, ".shelf", FileName);

    /// <summary>Creates an empty index with the current schema. Fails if the file already exists.</summary>
    /// <exception cref="IndexException"/>
    public static void Create(string path)
    {
        if (File.Exists(path))
        {
            throw new IndexException($"{path}: index already exists");
        }

        using var connection = Open(path, SqliteOpenMode.ReadWriteCreate);
        using var transaction = connection.BeginTransaction();
        CreateSchema(connection);
        transaction.Commit();
        Checkpoint(connection);
    }

    /// <summary>Opens an existing index for reading and writing.</summary>
    /// <exception cref="IndexException">The file is missing, or its schema version is not the current one.</exception>
    public static SqliteConnection OpenWrite(string path) => OpenExisting(path, SqliteOpenMode.ReadWrite);

    /// <summary>
    /// Opens an existing index for reading. The connection is read-write at file level but <c>query_only</c>:
    /// a truly read-only connection cannot checkpoint, so it would leave the WAL and shared-memory files behind.
    /// </summary>
    /// <exception cref="IndexException">The file is missing, or its schema version is not the current one.</exception>
    public static SqliteConnection OpenRead(string path)
    {
        var connection = OpenExisting(path, SqliteOpenMode.ReadWrite);
        Execute(connection, "PRAGMA query_only = ON");
        return connection;
    }

    /// <summary>Opens the index for a rebuild: creates it if missing, accepts any schema version (the rebuild replaces it).</summary>
    internal static SqliteConnection OpenForRebuild(string path) => Open(path, SqliteOpenMode.ReadWriteCreate);

    /// <summary>The schema version stored in the file (0 for a new, empty database).</summary>
    internal static int GetVersion(SqliteConnection connection) => (int)(long)Scalar(connection, "PRAGMA user_version")!;

    internal static void CreateSchema(SqliteConnection connection)
    {
        foreach (var statement in IndexSchema.Statements)
        {
            Execute(connection, statement);
        }

        Execute(connection, $"PRAGMA user_version = {IndexSchema.Version}");
    }

    /// <summary>Removes every table, trigger and index so that <see cref="CreateSchema"/> can start from nothing.</summary>
    internal static void DropSchema(SqliteConnection connection)
    {
        // Triggers first (they reference the FTS tables), then the FTS tables, then the tables children first:
        // with foreign keys on, dropping a parent table deletes its rows and needs the child tables to exist.
        foreach (var trigger in Names(connection, "trigger"))
        {
            Execute(connection, $"DROP TRIGGER IF EXISTS {Quote(trigger)}");
        }

        foreach (var table in new[] { "chunks_fts", "works_fts" }.Concat(IndexSchema.DataTablesChildrenFirst))
        {
            Execute(connection, $"DROP TABLE IF EXISTS {Quote(table)}");
        }

        // Whatever an older or newer version left behind. Shadow tables of FTS5 go with their virtual table.
        foreach (var table in Names(connection, "table"))
        {
            Execute(connection, $"DROP TABLE IF EXISTS {Quote(table)}");
        }
    }

    private static List<string> Names(SqliteConnection connection, string type)
    {
        var names = new List<string>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = $type AND name NOT LIKE 'sqlite_%'";
        command.Parameters.AddWithValue("$type", type);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    private static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

    /// <summary>
    /// Copies the WAL into the database and truncates it. Waits for readers up to the busy timeout and
    /// then gives up quietly: the log is only larger than necessary, nothing is lost.
    /// </summary>
    public static void Checkpoint(SqliteConnection connection)
    {
        try
        {
            Execute(connection, "PRAGMA wal_checkpoint(TRUNCATE)");
        }
        catch (SqliteException e) when (e.SqliteErrorCode == 5)
        {
            // SQLITE_BUSY: readers hold the log. The automatic checkpoint will catch up.
        }
    }

    internal static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    internal static object? Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    private static SqliteConnection OpenExisting(string path, SqliteOpenMode mode)
    {
        if (!File.Exists(path))
        {
            throw new IndexException($"{path}: no index found; run 'shelf reindex'");
        }

        var connection = Open(path, mode);
        try
        {
            var version = GetVersion(connection);
            if (version != IndexSchema.Version)
            {
                throw new IndexException(
                    $"{path}: index has schema version {version}, this shelf uses {IndexSchema.Version}; run 'shelf reindex'");
            }

            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private static SqliteConnection Open(string path, SqliteOpenMode mode)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = mode,
            Pooling = false,
            DefaultTimeout = BusyTimeoutMilliseconds / 1000,
        }.ToString());
        try
        {
            connection.Open();
            if (mode != SqliteOpenMode.ReadOnly)
            {
                // Persists in the file; a read-only connection cannot change it and does not need to.
                Execute(connection, "PRAGMA journal_mode = WAL");
                Execute(connection, "PRAGMA synchronous = NORMAL");
                Execute(connection, $"PRAGMA journal_size_limit = {JournalSizeLimitBytes}");
            }

            // Microsoft.Data.Sqlite retries on "Default Timeout" itself; the pragma makes SQLite wait as well.
            Execute(connection, $"PRAGMA busy_timeout = {BusyTimeoutMilliseconds}");
            Execute(connection, "PRAGMA foreign_keys = ON");
            return connection;
        }
        catch (SqliteException e)
        {
            connection.Dispose();
            throw new IndexException($"{path}: cannot open the index: {e.Message}", e);
        }
    }
}
