using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Proto;

public static class Wal
{
    private static string F(double v) => v.ToString("F2", CultureInfo.InvariantCulture);

    public static int Writer(Args a, TextWriter o)
    {
        var db = a.Positional[0];
        var seconds = a.Int("seconds", 30);
        var batch = a.Int("batch", 50);
        var id = a.Int("id", 1);
        var rng = new Rng((ulong)(1000 + id));
        using var c = Db.Open(db);
        var max = Convert.ToInt32(Db.Scalar(c, "SELECT max(id) FROM works"), CultureInfo.InvariantCulture);
        var waits = new List<double>();
        var ops = new List<double>();
        var failed = 0;
        long maxWal = 0;
        string? firstError = null;
        var end = Environment.TickCount64 + (seconds * 1000L);
        while (Environment.TickCount64 < end)
        {
            var clock = new Clock();
            try
            {
                using var tx = c.BeginTransaction(deferred: false);
                waits.Add(clock.Ms);
                using var upd = c.CreateCommand();
                upd.Transaction = tx;
                upd.CommandText = "UPDATE works SET description = (SELECT description FROM works WHERE id = $o) WHERE id = $i";
                var po = upd.Parameters.Add("$o", SqliteType.Integer);
                var pi = upd.Parameters.Add("$i", SqliteType.Integer);
                using var fts = c.CreateCommand();
                fts.Transaction = tx;
                fts.CommandText = "UPDATE works_fts SET description = (SELECT description FROM works WHERE id = $i) WHERE rowid = $i";
                var pfi = fts.Parameters.Add("$i", SqliteType.Integer);
                for (var n = 0; n < batch; n++)
                {
                    var target = 1 + rng.NextInt(max);
                    po.Value = 1 + rng.NextInt(max);
                    pi.Value = target;
                    upd.ExecuteNonQuery();
                    pfi.Value = target;
                    fts.ExecuteNonQuery();
                }

                tx.Commit();
                ops.Add(clock.Ms);
            }
            catch (SqliteException e)
            {
                failed++;
                firstError ??= $"{e.SqliteErrorCode}/{e.SqliteExtendedErrorCode}: {e.Message}";
            }

            maxWal = Math.Max(maxWal, Db.FileBytes(db + "-wal"));
            Thread.Sleep(10);
        }

        Out.Kv(o, "wal-writer", ("id", id), ("ops", ops.Count), ("failed", failed),
            ("wait_p95_ms", waits.Count > 0 ? F(Stats.Percentile(waits.ToArray(), 0.95)) : "n/a"),
            ("op_p95_ms", ops.Count > 0 ? F(Stats.Percentile(ops.ToArray(), 0.95)) : "n/a"),
            ("max_wal_bytes", maxWal));
        if (firstError != null)
        {
            o.WriteLine("wal-writer first_error " + firstError);
        }

        return 0;
    }

    public static int Reader(Args a, TextWriter o)
    {
        var db = a.Positional[0];
        var seconds = a.Int("seconds", 30);
        var queries = Fts.ReadQueries(a.Str("queries")).Select(q => q.Query).ToArray();
        var rng = new Rng((ulong)(7 + Environment.ProcessId));
        using var c = Db.Open(db);
        using var cmd = c.CreateCommand();
        cmd.CommandText = Fts.SearchSql(false, 20);
        var p = cmd.Parameters.Add("$q", SqliteType.Text);
        var lat = new List<double>();
        var failed = 0;
        string? firstError = null;
        var sleepMs = a.Int("sleep-ms", 0);
        var end = Environment.TickCount64 + (seconds * 1000L);
        while (Environment.TickCount64 < end)
        {
            var clock = new Clock();
            try
            {
                p.Value = queries[rng.NextInt(queries.Length)];
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    _ = r.GetString(1);
                }

                lat.Add(clock.Ms);
            }
            catch (SqliteException e)
            {
                failed++;
                firstError ??= $"{e.SqliteErrorCode}/{e.SqliteExtendedErrorCode}: {e.Message}";
            }

            if (sleepMs > 0)
            {
                Thread.Sleep(sleepMs);
            }
        }

        Out.Kv(o, "wal-reader", ("ops", lat.Count), ("failed", failed),
            ("p50_ms", F(Stats.Median(lat.ToArray()))), ("p95_ms", F(Stats.Percentile(lat.ToArray(), 0.95))), ("max_ms", F(lat.Max())));
        if (firstError != null)
        {
            o.WriteLine("wal-reader first_error " + firstError);
        }

        return 0;
    }

    public static int Hold(Args a, TextWriter o)
    {
        using var c = Db.Open(a.Positional[0]);
        using var tx = c.BeginTransaction(deferred: false);
        Db.Exec(c, "UPDATE works SET year = year WHERE id = 1");
        o.WriteLine("wal-hold holding");
        o.Flush();
        Thread.Sleep(a.Int("seconds", 6) * 1000);
        tx.Commit();
        o.WriteLine("wal-hold released");
        return 0;
    }

    public static int Try(Args a, TextWriter o)
    {
        var clock = new Clock();
        try
        {
            using var c = Db.Open(a.Positional[0]);
            using var tx = c.BeginTransaction(deferred: false);
            tx.Commit();
            o.WriteLine(FormattableString.Invariant($"wal-try acquired after {clock.Ms:F0} ms"));
        }
        catch (SqliteException e)
        {
            o.WriteLine(FormattableString.Invariant($"wal-try failed after {clock.Ms:F0} ms: {e.GetType().Name} SqliteErrorCode={e.SqliteErrorCode} SqliteExtendedErrorCode={e.SqliteExtendedErrorCode} message={e.Message}"));
        }

        return 0;
    }
}
