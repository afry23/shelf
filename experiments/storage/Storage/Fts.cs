using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Proto;

public static class Fts
{
    private const string Create =
        "CREATE VIRTUAL TABLE works_fts USING fts5(title, subtitle, authors, description, tags, tokenize = 'unicode61 remove_diacritics 2')";

    public static int Info(TextWriter o)
    {
        using var c = new SqliteConnection("Data Source=:memory:");
        c.Open();
        o.WriteLine("sqlite_version=" + Db.Scalar(c, "SELECT sqlite_version()"));
        o.WriteLine("rid=" + System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier);
        o.WriteLine("os=" + System.Runtime.InteropServices.RuntimeInformation.OSDescription);
        o.WriteLine("arch=" + System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture);
        o.WriteLine("base_directory=" + AppContext.BaseDirectory);
        using var cmd = c.CreateCommand();
        cmd.CommandText = "PRAGMA compile_options";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            o.WriteLine("compile_option=" + r.GetString(0));
        }

        return 0;
    }

    private sealed class Runner(TextWriter o)
    {
        public int Passed;
        public int Gated;

        public void Gate(string id, bool ok, string detail = "")
        {
            Gated++;
            if (ok)
            {
                Passed++;
                o.WriteLine("pass " + id);
            }
            else
            {
                o.WriteLine($"fail {id}: {detail}");
            }
        }

        public void Observe(string id, string value) => o.WriteLine($"observed {id}: {value}");
    }

    private static void Insert(SqliteConnection c, long id, string title = "", string subtitle = "", string authors = "", string description = "", string tags = "")
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = "INSERT INTO works_fts(rowid, title, subtitle, authors, description, tags) VALUES ($i, $t, $s, $a, $d, $g)";
        cmd.Parameters.AddWithValue("$i", id);
        cmd.Parameters.AddWithValue("$t", title);
        cmd.Parameters.AddWithValue("$s", subtitle);
        cmd.Parameters.AddWithValue("$a", authors);
        cmd.Parameters.AddWithValue("$d", description);
        cmd.Parameters.AddWithValue("$g", tags);
        cmd.ExecuteNonQuery();
    }

    private static List<long> Hits(SqliteConnection c, string match, string table = "works_fts")
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT rowid FROM {table} WHERE {table} MATCH $q ORDER BY {(table == "works_fts" ? Db.Bm25 : "rank")}, rowid";
        cmd.Parameters.AddWithValue("$q", match);
        using var r = cmd.ExecuteReader();
        var list = new List<long>();
        while (r.Read())
        {
            list.Add(r.GetInt64(0));
        }

        return list;
    }

    private static string J(List<long> l) => string.Join(",", l);

    /// <summary>Creates a fresh DB with works_fts, runs setup, tears the DB down.</summary>
    private static void WithDb(Action<SqliteConnection> body, string create = Create)
    {
        var path = Db.TempDb();
        try
        {
            using var c = Db.Open(path);
            Db.Exec(c, create);
            body(c);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Db.DeleteDb(path);
        }
    }

    public static int Check(TextWriter o)
    {
        var failures = SelfCheck.Run(o);
        var t = new Runner(o);

        using (var c = new SqliteConnection("Data Source=:memory:"))
        {
            c.Open();
            var v = Convert.ToString(Db.Scalar(c, "SELECT sqlite_version()"), CultureInfo.InvariantCulture);
            t.Gate("F1", true);
            o.WriteLine($"observed F1: sqlite_version={v} (expected 3.53.3, {(v == "3.53.3" ? "equal" : "DIFFERENT")})");
            using var cmd = c.CreateCommand();
            cmd.CommandText = "PRAGMA compile_options";
            var opts = new List<string>();
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    opts.Add(r.GetString(0));
                }
            }

            t.Gate("F2", opts.Contains("ENABLE_FTS5") && !opts.Contains("OMIT_LOAD_EXTENSION"), string.Join(" ", opts));
        }

        var f3 = true;
        try
        {
            WithDb(_ => { });
        }
        catch (Exception e)
        {
            f3 = false;
            o.WriteLine("  " + e.Message);
        }

        t.Gate("F3", f3);

        WithDb(c =>
        {
            Insert(c, 1, title: "Kubernetes Patterns");
            Insert(c, 2, description: "... deploying on kubernetes ...");
            var h = Hits(c, "kubernetes");
            t.Gate("F4", J(h) == "1,2", J(h));
        });
        WithDb(c =>
        {
            Insert(c, 1, title: "Event Sourcing in Practice");
            Insert(c, 2, title: "Sourcing Events Daily");
            var h = Hits(c, "\"event sourcing\"");
            t.Gate("F5", J(h) == "1", J(h));
        });
        WithDb(c =>
        {
            Insert(c, 1, title: "Kubernetes Patterns");
            var h = Hits(c, "kube*");
            t.Gate("F6", h.Count == 1, J(h));
        });
        WithDb(c =>
        {
            Insert(c, 1, title: "Kubernetes Patterns");
            Insert(c, 2, title: "Other", authors: "Kubernetes Team");
            var h = Hits(c, "title : kubernetes");
            t.Gate("F7", J(h) == "1", J(h));
        });
        WithDb(c =>
        {
            Insert(c, 1, title: "Cloud Patterns", tags: "cloud/azure");
            Insert(c, 2, title: "Cloud Native", tags: "cloud/aws");
            var h = Hits(c, "cloud NOT azure");
            t.Gate("F8", J(h) == "2", J(h));
        });
        WithDb(c =>
        {
            Insert(c, 1, title: "Designing Data-Intensive Applications");
            using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT snippet(works_fts, 0, '[', ']', '...', 8) FROM works_fts WHERE works_fts MATCH 'data'";
            var s = Convert.ToString(cmd.ExecuteScalar(), CultureInfo.InvariantCulture) ?? "";
            t.Gate("F9", s.Contains("[Data]", StringComparison.Ordinal), s);
        });
        WithDb(c =>
        {
            Insert(c, 1, authors: "Thomas Müller");
            var h = Hits(c, "muller");
            t.Gate("F10", h.Count == 1, J(h));
            h = Hits(c, "MÜLLER");
            t.Gate("F11", h.Count == 1, J(h));
            o.WriteLine("observed F15: mueller hits=" + Hits(c, "mueller").Count + " (expected 0)");
        });
        WithDb(c =>
        {
            Insert(c, 1, authors: "Jördis Ålander");
            var a = Hits(c, "alander").Count;
            var b = Hits(c, "jordis").Count;
            t.Gate("F12", a == 1 && b == 1, $"{a},{b}");
        });
        WithDb(c =>
        {
            Insert(c, 1, title: "Café Société");
            var h = Hits(c, "cafe societe");
            t.Gate("F13", h.Count == 1, J(h));
        });
        WithDb(
            c =>
            {
                Insert(c, 1, authors: "Thomas Müller");
                var h = Hits(c, "muller");
                t.Gate("F14", h.Count == 0, J(h));
            },
            Create.Replace("remove_diacritics 2", "remove_diacritics 0", StringComparison.Ordinal));
        WithDb(c =>
        {
            Insert(c, 1, title: "Größenordnungen verteilter Systeme");
            t.Observe("F16", $"grossenordnungen={Hits(c, "grossenordnungen").Count} großenordnungen={Hits(c, "großenordnungen").Count} grössenordnungen={Hits(c, "grössenordnungen").Count} (expected 0/1/0)");
        });
        WithDb(c =>
        {
            Insert(c, 1, title: "Die Straße");
            t.Observe("F17", $"strasse={Hits(c, "strasse").Count} STRASSE={Hits(c, "STRASSE").Count} straße={Hits(c, "straße").Count} (expected 0/0/1)");
        });
        WithDb(c =>
        {
            Insert(c, 1, tags: "architektur/ddd");
            t.Observe("F18", $"architektur={Hits(c, "architektur").Count} ddd={Hits(c, "ddd").Count} \"architektur ddd\"={Hits(c, "\"architektur ddd\"").Count} (expected 1/1/1)");
        });
        try
        {
            var path = Db.TempDb();
            using (var c = Db.Open(path))
            {
                Db.Exec(c, "CREATE VIRTUAL TABLE t USING fts5(x, tokenize='trigram')");
                Db.Exec(c, "INSERT INTO t(x) VALUES ('Kubernetes')");
                t.Observe("F19", "trigram available, 'ubernet' hits=" + Db.Scalar(c, "SELECT count(*) FROM t WHERE t MATCH 'ubernet'"));
            }

            SqliteConnection.ClearAllPools();
            Db.DeleteDb(path);
        }
        catch (SqliteException e)
        {
            t.Observe("F19", "trigram NOT available: " + e.Message);
        }

        {
            var path = Db.TempDb();
            string first;
            string second;
            using (var c = Db.Open(path))
            {
                first = Convert.ToString(Db.Scalar(c, "PRAGMA journal_mode"), CultureInfo.InvariantCulture) ?? "";
            }

            using (var c = new SqliteConnection($"Data Source={path};Pooling=False"))
            {
                c.Open();
                second = Convert.ToString(Db.Scalar(c, "PRAGMA journal_mode"), CultureInfo.InvariantCulture) ?? "";
            }

            t.Gate("F20", first == "wal" && second == "wal", $"{first},{second}");
            SqliteConnection.ClearAllPools();
            Db.DeleteDb(path);
        }

        o.WriteLine($"passed {t.Passed}/{t.Gated}");
        return failures + (t.Passed == t.Gated ? 0 : 1) > 0 ? 1 : 0;
    }

    // ---- search and benchmark on a loaded database ----

    public static string SearchSql(bool content, int limit) => content
        ? $"SELECT chunks.id, snippet(chunks_fts, 0, '[', ']', '...', 12) FROM chunks_fts JOIN chunks ON chunks.id = chunks_fts.rowid WHERE chunks_fts MATCH $q ORDER BY rank LIMIT {limit}"
        : $"SELECT works.id, works.title FROM works_fts JOIN works ON works.id = works_fts.rowid WHERE works_fts MATCH $q ORDER BY {Db.Bm25} LIMIT {limit}";

    public static int Search(Args a, TextWriter o)
    {
        var clock = new Clock();
        using var c = Db.Open(a.Positional[0]);
        var open = clock.Ms;
        using var cmd = c.CreateCommand();
        cmd.CommandText = SearchSql(a.Has("content"), a.Int("limit", 20));
        cmd.Parameters.AddWithValue("$q", a.Positional[1]);
        var n = 0;
        using (var r = cmd.ExecuteReader())
        {
            while (r.Read())
            {
                o.WriteLine($"{r.GetInt64(0)}\t{r.GetString(1)}");
                n++;
            }
        }

        Out.Kv(o, "search", ("hits", n), ("open_ms", open.ToString("F2", CultureInfo.InvariantCulture)), ("total_ms", clock.Ms.ToString("F2", CultureInfo.InvariantCulture)));
        return 0;
    }

    public static List<(string Bucket, string Query)> ReadQueries(params string[] files)
    {
        var list = new List<(string, string)>();
        foreach (var f in files.Where(File.Exists))
        {
            foreach (var line in File.ReadAllLines(f).Where(l => l.Length > 0 && !l.StartsWith('#')))
            {
                var tab = line.IndexOf('\t', StringComparison.Ordinal);
                list.Add(tab < 0 ? ("fixed", line) : (line[..tab], line[(tab + 1)..]));
            }
        }

        return list;
    }

    public static int Bench(Args a, TextWriter o)
    {
        var content = a.Has("content");
        var queries = ReadQueries(a.Positional.Skip(1).ToArray());
        using var c = Db.Open(a.Positional[0]);
        using var cmd = c.CreateCommand();
        cmd.CommandText = SearchSql(content, 20);
        var p = cmd.Parameters.Add("$q", SqliteType.Text);
        var all = new List<double>();
        var byBucket = new Dictionary<string, List<double>>();
        var hits = 0L;

        int Run(string q)
        {
            p.Value = q;
            var n = 0;
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                _ = r.GetInt64(0);
                _ = r.GetString(1);
                n++;
            }

            return n;
        }

        for (var i = 0; i < 10; i++)
        {
            _ = Run(queries[i % queries.Count].Query);
        }

        foreach (var (bucket, q) in queries)
        {
            for (var rep = 0; rep < 20; rep++)
            {
                var clock = new Clock();
                hits += Run(q);
                var ms = clock.Ms;
                all.Add(ms);
                if (!byBucket.TryGetValue(bucket, out var l))
                {
                    byBucket[bucket] = l = [];
                }

                l.Add(ms);
            }
        }

        var mode = content ? "chunks" : "works";
        o.WriteLine(FormattableString.Invariant($"fts-bench {mode} overall {Stats.Summary(all.ToArray())} queries={queries.Count} avg_hits={(double)hits / all.Count:F1}"));
        foreach (var (bucket, l) in byBucket.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            o.WriteLine($"fts-bench {mode} bucket={bucket} {Stats.Summary(l.ToArray())}");
        }

        return 0;
    }
}
