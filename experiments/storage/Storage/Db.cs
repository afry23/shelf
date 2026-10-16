using Microsoft.Data.Sqlite;

namespace Proto;

public static class Db
{
    public const string Bm25 = "bm25(works_fts, 10.0, 5.0, 8.0, 1.0, 3.0)";

    public static SqliteConnection Open(string path)
    {
        var c = new SqliteConnection($"Data Source={path};Default Timeout=5;Pooling=False");
        c.Open();
        Exec(c, "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA busy_timeout=5000;");
        return c;
    }

    public static void Exec(SqliteConnection c, string sql)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    public static object? Scalar(SqliteConnection c, string sql)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        return cmd.ExecuteScalar();
    }

    public static long FileBytes(string path) => File.Exists(path) ? new FileInfo(path).Length : 0;

    public static string TempDb()
    {
        var dir = Path.Combine(Path.GetTempPath(), "shelf-spike");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, Guid.NewGuid().ToString("N") + ".db");
    }

    public static void DeleteDb(string path)
    {
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            File.Delete(path + suffix);
        }
    }
}

public sealed class Args
{
    private static readonly HashSet<string> Flags = ["content", "large", "v3d"];
    public List<string> Positional { get; } = [];
    private readonly Dictionary<string, string> _opts = [];

    public Args(string[] args, int start)
    {
        for (var i = start; i < args.Length; i++)
        {
            if (args[i].StartsWith("--", StringComparison.Ordinal))
            {
                var name = args[i][2..];
                _opts[name] = Flags.Contains(name) ? "true" : args[++i];
            }
            else
            {
                Positional.Add(args[i]);
            }
        }
    }

    public string Str(string name, string def = "") => _opts.TryGetValue(name, out var v) ? v : def;
    public int Int(string name, int def) => _opts.TryGetValue(name, out var v) ? int.Parse(v, System.Globalization.CultureInfo.InvariantCulture) : def;
    public bool Has(string name) => _opts.ContainsKey(name);
}
