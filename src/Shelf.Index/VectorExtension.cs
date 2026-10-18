using Microsoft.Data.Sqlite;

namespace Shelf.Index;

/// <summary>Loads the sqlite-vec extension (<c>vec0</c>) into a connection (F-SEM-4, ADR 0004).</summary>
public interface IVectorExtension
{
    /// <summary>True when the extension is loaded afterwards. A missing or incompatible library is no error.</summary>
    bool TryLoad(SqliteConnection connection);
}

/// <summary>
/// The library <c>vec0.dylib</c> or <c>vec0.so</c> next to the executable, loaded by absolute path (never by a bare
/// name, never from the working directory). The build puts it there from <c>vendor/sqlite-vec/&lt;rid&gt;/</c>
/// (<c>tools/fetch-sqlite-vec.sh</c> fetches it with a pinned checksum).
/// </summary>
public sealed class SqliteVecExtension : IVectorExtension
{
    public static SqliteVecExtension Default { get; } = new();

    public static string LibraryPath { get; } =
        Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsMacOS() ? "vec0.dylib" : "vec0.so");

    public bool TryLoad(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (!File.Exists(LibraryPath))
        {
            return false;
        }

        try
        {
            connection.EnableExtensions(true);
            connection.LoadExtension(LibraryPath);
            return true;
        }
        catch (SqliteException)
        {
            return false;
        }
        finally
        {
            connection.EnableExtensions(false);
        }
    }
}

/// <summary>An extension that is never available; vectors are then kept in plain tables.</summary>
public sealed class NoVectorExtension : IVectorExtension
{
    public static NoVectorExtension Instance { get; } = new();

    public bool TryLoad(SqliteConnection connection) => false;
}

/// <summary>The vector tables and their bookkeeping inside <c>index.db</c>. Not part of <see cref="IndexSchema"/>: they come and go with the embeddings.</summary>
public static class VectorTables
{
    public const string Works = "vec_works";
    public const string Chunks = "vec_chunks";
    public const string Meta = "semantic_meta";

    /// <summary>Hash of the text each work vector was made from, so that unchanged works are not embedded again.</summary>
    public const string WorkHashes = "semantic_work_hash";

    /// <summary>True when a virtual table of sqlite-vec exists: the file cannot be used without the library.</summary>
    public static bool NeedsExtension(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM sqlite_master WHERE name IN ('vec_works', 'vec_chunks') AND sql LIKE 'CREATE VIRTUAL TABLE%'";
        return (long)command.ExecuteScalar()! > 0;
    }

    /// <summary>Drops the vector tables and the bookkeeping. With sqlite-vec tables, the extension must be loaded.</summary>
    public static void Drop(SqliteConnection connection)
    {
        foreach (var table in new[] { Works, Chunks, Meta, WorkHashes })
        {
            IndexDatabase.Execute(connection, $"DROP TABLE IF EXISTS {table}");
        }
    }
}
