using System.Runtime.InteropServices;
using Microsoft.Data.Sqlite;

namespace Proto;

internal static class DbCommand
{
    public static int Check(string? vecPath, TextWriter stdout, TextWriter stderr)
    {
        var ext = vecPath ?? Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsMacOS() ? "vec0.dylib" : "vec0.so");
        using var c = new SqliteConnection("Data Source=:memory:");
        c.Open();

        stdout.WriteLine("sqlite_version=" + Scalar(c, "SELECT sqlite_version()"));

        // FTS5 with bm25 and per-column weights (title weighs more than body).
        Exec(c, "CREATE VIRTUAL TABLE works_fts USING fts5(title, body, tokenize = 'unicode61 remove_diacritics 2')");
        Exec(c, "INSERT INTO works_fts(rowid, title, body) VALUES (1, 'Event Streams in Practice', 'streams streams logs'), (2, 'Stream Processing', 'streams'), (3, 'Quiet Systems', 'calm')");
        var ids = new List<string>();
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "SELECT rowid FROM works_fts WHERE works_fts MATCH 'streams' ORDER BY bm25(works_fts, 10.0, 1.0)";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                ids.Add(r.GetInt64(0).ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        stdout.WriteLine("fts5 match=streams order=" + string.Join(",", ids));

        // sqlite-vec as a loadable extension next to the binary.
        c.LoadExtension(ext);
        stdout.WriteLine("vec_version=" + Scalar(c, "SELECT vec_version()"));
        Exec(c, "CREATE VIRTUAL TABLE v USING vec0(embedding float[4])");
        InsertVector(c, 1, [1f, 0f, 0f, 0f]);
        InsertVector(c, 2, [0f, 1f, 0f, 0f]);
        InsertVector(c, 3, [0f, 0f, 1f, 0f]);
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "SELECT rowid FROM v WHERE embedding MATCH $q AND k = 1";
            cmd.Parameters.AddWithValue("$q", ToBlob([0.9f, 0.1f, 0f, 0f]));
            stdout.WriteLine("knn nearest=" + cmd.ExecuteScalar());
        }

        return 0;
    }

    private static byte[] ToBlob(float[] v) => MemoryMarshal.AsBytes<float>(v).ToArray();

    private static void InsertVector(SqliteConnection c, long id, float[] v)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = "INSERT INTO v(rowid, embedding) VALUES ($id, $e)";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$e", ToBlob(v));
        cmd.ExecuteNonQuery();
    }

    private static void Exec(SqliteConnection c, string sql)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static object? Scalar(SqliteConnection c, string sql)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        return cmd.ExecuteScalar();
    }
}
