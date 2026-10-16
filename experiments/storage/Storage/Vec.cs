using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Proto;

public static class Vec
{
    private static SqliteConnection Mem()
    {
        var c = new SqliteConnection("Data Source=:memory:");
        c.Open();
        return c;
    }

    private static string Describe(SqliteException e) => $"{e.GetType().Name} SqliteErrorCode={e.SqliteErrorCode} message={e.Message.ReplaceLineEndings(" ")}";

    private static string TryLoad(string file, string? proc, bool enable)
    {
        try
        {
            using var c = Mem();
            if (enable)
            {
                c.EnableExtensions(true);
            }

            c.LoadExtension(file, proc);
            return "ok vec_version=" + Db.Scalar(c, "SELECT vec_version()");
        }
        catch (SqliteException e)
        {
            return "error " + Describe(e);
        }
    }

    /// <summary>ext-try &lt;path&gt; [proc]: one load attempt, prints ok/error (V3d, V9 are driven by the scripts).</summary>
    public static int ExtTry(Args a, TextWriter o)
    {
        o.WriteLine("ext-try " + TryLoad(a.Positional[0], a.Positional.Count > 1 ? a.Positional[1] : null, true));
        return 0;
    }

    public static int Check(Args a, TextWriter o)
    {
        var ext = a.Str("ext");
        var fails = Bf.SelfCheck(o);
        var gated = 0;
        var passed = 0;

        void Gate(string id, bool ok, string detail = "")
        {
            gated++;
            if (ok)
            {
                passed++;
                o.WriteLine("pass " + id);
            }
            else
            {
                o.WriteLine($"fail {id}: {detail}");
            }
        }

        var r = TryLoad(ext, null, true);
        Gate("V1", r.StartsWith("ok vec_version=v0.1.9", StringComparison.Ordinal), r);

        o.WriteLine("observed V3a: absolute path without suffix: " + TryLoad(Path.ChangeExtension(ext, null), null, true));

        var next = Path.Combine(AppContext.BaseDirectory, "vec0");
        r = TryLoad(next, null, true);
        Gate("V3b", r.StartsWith("ok", StringComparison.Ordinal), r);

        var cwd = Environment.CurrentDirectory;
        Environment.CurrentDirectory = Path.GetTempPath();
        o.WriteLine("observed V3c: bare name 'vec0', library next to binary, cwd elsewhere: " + TryLoad("vec0", null, true));
        Environment.CurrentDirectory = cwd;

        var p1 = TryLoad(ext, null, true);
        var p2 = TryLoad(ext, "sqlite3_vec_init", true);
        Gate("V4", p1.StartsWith("ok", StringComparison.Ordinal) && p2.StartsWith("ok", StringComparison.Ordinal), p1 + " | " + p2);

        try
        {
            using var c = Mem();
            c.EnableExtensions(true);
            c.LoadExtension(Path.Combine(Path.GetTempPath(), "does-not-exist", "vec0"));
            Gate("V5", false, "no exception");
        }
        catch (SqliteException e)
        {
            var after = TryLoad(ext, null, true);
            Gate("V5", after.StartsWith("ok", StringComparison.Ordinal), "process continues: " + after);
            o.WriteLine("observed V5: " + Describe(e));
        }

        var (v2, d2) = KnnCheck(ext, 384, 1000, 20);
        Gate("V2", v2, d2);
        var (v6, d6) = KnnCheck(ext, 1024, 1000, 20);
        Gate("V6", v6, d6);

        try
        {
            using var c = Mem();
            VecData.LoadExt(c, ext);
            Db.Exec(c, "CREATE VIRTUAL TABLE v USING vec0(id INTEGER PRIMARY KEY, embedding float[384] distance_metric=cosine)");
            using var cmd = c.CreateCommand();
            cmd.CommandText = "INSERT INTO v(id, embedding) VALUES (1, $e)";
            cmd.Parameters.AddWithValue("$e", new byte[383 * 4]);
            cmd.ExecuteNonQuery();
            o.WriteLine("observed V7: 383 floats accepted (unexpected)");
        }
        catch (SqliteException e)
        {
            o.WriteLine("observed V7: " + Describe(e));
        }

        o.WriteLine("observed V8: LoadExtension without EnableExtensions(true): " + TryLoad(ext, null, false));

        o.WriteLine($"passed {passed}/{gated}");
        return fails + (passed == gated ? 0 : 1) > 0 ? 1 : 0;
    }

    private static (bool Ok, string Detail) KnnCheck(string ext, int dim, int count, int queries)
    {
        try
        {
            using var c = Mem();
            VecData.LoadExt(c, ext);
            Db.Exec(c, $"CREATE VIRTUAL TABLE vec_chunks USING vec0(chunk_id INTEGER PRIMARY KEY, embedding float[{dim}] distance_metric=cosine)");
            var rng = new Rng(5);
            var data = new float[count * dim];
            using (var ins = c.CreateCommand())
            {
                ins.CommandText = "INSERT INTO vec_chunks(chunk_id, embedding) VALUES ($i, $e)";
                var pi = ins.Parameters.Add("$i", SqliteType.Integer);
                var pe = ins.Parameters.Add("$e", SqliteType.Blob);
                for (var i = 0; i < count; i++)
                {
                    var row = data.AsSpan(i * dim, dim);
                    Vectors.FillUnit(rng, row);
                    pi.Value = i + 1;
                    pe.Value = Vectors.ToBytes(row).ToArray();
                    ins.ExecuteNonQuery();
                }
            }

            for (var qi = 0; qi < queries; qi++)
            {
                var q = VecData.Query(qi, dim);
                var got = VecData.VecKnn(c, "chunks", q);
                var want = Bf.Reference(data, count, dim, q, 10, null);
                if (!Bf.Same(got, want, out var detail))
                {
                    return (false, $"dim {dim} query {qi}: {detail}");
                }
            }

            return (true, $"dim {dim}: {queries} queries equal to the reference");
        }
        catch (SqliteException e)
        {
            return (false, Describe(e));
        }
    }
}
