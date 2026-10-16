using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Proto;

/// <summary>Synthetic data: works from ground-truth.json, chunks from the generator's vocabularies.</summary>
public static class Data
{
    private static readonly string[] TopTags = ["architecture", "cloud", "data", "languages", "security", "process", "ai", "web"];
    private static readonly string[] SubTags = ["fundamentals", "patterns", "tools", "testing", "operations"];

    private sealed class Vocab
    {
        public required string[] Words;
        public required ulong[] Cumulative;

        public static Vocab Load(string file)
        {
            var words = File.ReadAllLines(file).Select(l => l.Trim()).Where(l => l.Length > 0).ToArray();
            var cum = new ulong[words.Length];
            ulong total = 0;
            for (var i = 0; i < words.Length; i++)
            {
                total += 1_000_000_000UL / (ulong)(i + 1);
                cum[i] = total;
            }

            return new Vocab { Words = words, Cumulative = cum };
        }

        public string Pick(Rng rng)
        {
            var r = rng.NextULong() % Cumulative[^1];
            var idx = Array.BinarySearch(Cumulative, r);
            if (idx < 0)
            {
                idx = ~idx;
            }
            else
            {
                idx++;
            }

            return Words[Math.Min(idx, Words.Length - 1)];
        }
    }

    private static SqliteParameter[] Params(SqliteCommand cmd, params string[] specs) =>
        specs.Select(sp => sp.EndsWith(":I", StringComparison.Ordinal)
            ? cmd.Parameters.Add(sp[..^2], SqliteType.Integer)
            : cmd.Parameters.Add(sp, SqliteType.Text)).ToArray();

    private static ulong Fnv(string s)
    {
        var h = 14695981039346656037UL;
        foreach (var ch in Encoding.UTF8.GetBytes(s))
        {
            h = (h ^ ch) * 1099511628211UL;
        }

        return h;
    }

    private static string Words(Vocab v, Rng rng, int count)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < count; i++)
        {
            if (i > 0)
            {
                sb.Append(' ');
            }

            sb.Append(v.Pick(rng));
        }

        return sb.ToString();
    }

    public static int Load(Args a, TextWriter o)
    {
        var gtPath = a.Positional[0];
        var dbPath = a.Positional[1];
        var vocabDir = a.Str("vocab");
        var chunkTotal = a.Int("chunks", 100_000);
        var seed = (ulong)a.Int("seed", 42);
        var clock = new Clock();

        var gt = JsonSerializer.Deserialize(File.ReadAllText(gtPath), GtContext.Default.GroundTruth)
            ?? throw new InvalidOperationException("empty ground truth");
        var vocab = new Dictionary<string, Vocab>
        {
            ["en"] = Vocab.Load(Path.Combine(vocabDir, "en.txt")),
            ["de"] = Vocab.Load(Path.Combine(vocabDir, "de.txt")),
        };

        foreach (var s in new[] { "", "-wal", "-shm" })
        {
            File.Delete(dbPath + s);
        }

        using var c = Db.Open(dbPath);
        Db.Exec(c, "PRAGMA synchronous=OFF");
        Db.Exec(c, """
            CREATE TABLE works(id INTEGER PRIMARY KEY, title TEXT NOT NULL, subtitle TEXT, authors_json TEXT, language TEXT, year INTEGER, isbn13 TEXT, series TEXT, description TEXT, tags TEXT);
            CREATE TABLE chunks(id INTEGER PRIMARY KEY, file_id INTEGER NOT NULL, ordinal INTEGER NOT NULL, locator TEXT, text TEXT NOT NULL);
            CREATE VIRTUAL TABLE works_fts USING fts5(title, subtitle, authors, description, tags, tokenize = 'unicode61 remove_diacritics 2');
            CREATE VIRTUAL TABLE chunks_fts USING fts5(text, content='chunks', content_rowid='id', tokenize = 'unicode61 remove_diacritics 2');
            """);

        // works + works_fts
        var tx = c.BeginTransaction();
        using var insWork = c.CreateCommand();
        insWork.CommandText = "INSERT INTO works(id,title,subtitle,authors_json,language,year,isbn13,series,description,tags) VALUES ($i,$t,'',$aj,$l,$y,$isbn,$s,$d,$g)";
        var pw = Params(insWork, "$i:I", "$t", "$aj", "$l", "$y:I", "$isbn", "$s", "$d", "$g");
        using var insFts = c.CreateCommand();
        insFts.CommandText = "INSERT INTO works_fts(rowid,title,subtitle,authors,description,tags) VALUES ($i,$t,'',$a,$d,$g)";
        var pf = Params(insFts, "$i:I", "$t", "$a", "$d", "$g");
        var workLang = new string[gt.Works.Count];
        for (var i = 0; i < gt.Works.Count; i++)
        {
            var w = gt.Works[i];
            var lang = w.Language == "de" ? "de" : "en";
            workLang[i] = lang;
            var rng = new Rng(seed ^ Fnv(w.Key));
            var desc = Words(vocab[lang], rng, 30 + rng.NextInt(51));
            var tagCount = rng.NextInt(4);
            var tags = string.Join(' ', Enumerable.Range(0, tagCount).Select(_ => TopTags[rng.NextInt(TopTags.Length)] + "/" + SubTags[rng.NextInt(SubTags.Length)]).Distinct());
            var authors = string.Join(", ", w.Authors);
            var id = i + 1;
            pw[0].Value = id;
            pw[1].Value = w.Title;
            pw[2].Value = "[" + string.Join(",", w.Authors.Select(x => "\"" + x + "\"")) + "]";
            pw[3].Value = w.Language;
            pw[4].Value = w.Year;
            pw[5].Value = (object?)w.Isbn13 ?? DBNull.Value;
            pw[6].Value = (object?)w.Series?.Name ?? DBNull.Value;
            pw[7].Value = desc;
            pw[8].Value = tags;
            insWork.Transaction = tx;
            insWork.ExecuteNonQuery();
            pf[0].Value = id;
            pf[1].Value = w.Title;
            pf[2].Value = authors;
            pf[3].Value = desc;
            pf[4].Value = tags;
            insFts.Transaction = tx;
            insFts.ExecuteNonQuery();
            if (id % 1000 == 0)
            {
                tx.Commit();
                tx = c.BeginTransaction();
            }
        }

        tx.Commit();
        var worksMs = clock.Ms;

        // chunks + chunks_fts
        var perWork = (chunkTotal + gt.Works.Count - 1) / gt.Works.Count;
        tx = c.BeginTransaction();
        using var insChunk = c.CreateCommand();
        insChunk.CommandText = "INSERT INTO chunks(id,file_id,ordinal,locator,text) VALUES ($i,$f,$o,$loc,$t)";
        var pc = Params(insChunk, "$i:I", "$f:I", "$o:I", "$loc", "$t");
        using var insCf = c.CreateCommand();
        insCf.CommandText = "INSERT INTO chunks_fts(rowid,text) VALUES ($i,$t)";
        var pcf = Params(insCf, "$i:I", "$t");
        for (var id = 1; id <= chunkTotal; id++)
        {
            var wi = (id - 1) / perWork;
            var ordinal = (id - 1) % perWork;
            var rng = new Rng(seed ^ (0xC0FFEEUL * (ulong)id));
            var text = Words(vocab[workLang[wi]], rng, 400 + rng.NextInt(201));
            pc[0].Value = id;
            pc[1].Value = wi + 1;
            pc[2].Value = ordinal;
            pc[3].Value = "p" + (ordinal + 1).ToString(CultureInfo.InvariantCulture);
            pc[4].Value = text;
            insChunk.Transaction = tx;
            insChunk.ExecuteNonQuery();
            pcf[0].Value = id;
            pcf[1].Value = text;
            insCf.Transaction = tx;
            insCf.ExecuteNonQuery();
            if (id % 1000 == 0)
            {
                tx.Commit();
                tx = c.BeginTransaction();
            }
        }

        tx.Commit();
        var chunksMs = clock.Ms - worksMs;

        Db.Exec(c, "INSERT INTO works_fts(works_fts) VALUES('optimize'); INSERT INTO chunks_fts(chunks_fts) VALUES('optimize');");
        Db.Exec(c, "PRAGMA wal_checkpoint(TRUNCATE)");
        Db.Exec(c, "PRAGMA synchronous=NORMAL");

        WriteQueries(c, o, "works_fts", "works", "description", dbPath + ".queries.txt", seed);
        WriteQueries(c, o, "chunks_fts", "chunks", "text", dbPath + ".chunks.queries.txt", seed + 1);

        Out.Kv(o, "load",
            ("works", Db.Scalar(c, "SELECT count(*) FROM works")!),
            ("chunks", Db.Scalar(c, "SELECT count(*) FROM chunks")!),
            ("works_ms", worksMs.ToString("F0", CultureInfo.InvariantCulture)),
            ("chunks_ms", chunksMs.ToString("F0", CultureInfo.InvariantCulture)),
            ("total_ms", clock.Ms.ToString("F0", CultureInfo.InvariantCulture)),
            ("db_bytes", Db.FileBytes(dbPath)));
        return 0;
    }

    /// <summary>10 terms each from df 1-10, 100-300, >= 2000 (by document frequency) and 10 two-term ANDs; "bucket TAB query".</summary>
    private static void WriteQueries(SqliteConnection c, TextWriter o, string fts, string table, string column, string file, ulong seed)
    {
        Db.Exec(c, $"CREATE VIRTUAL TABLE IF NOT EXISTS temp.{fts}_v USING fts5vocab(main, {fts}, 'row')");
        var terms = new List<(string Term, long Doc)>();
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = $"SELECT term, doc FROM temp.{fts}_v ORDER BY term";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var t = r.GetString(0);
                if (t.All(char.IsLetter))
                {
                    terms.Add((t, r.GetInt64(1)));
                }
            }
        }

        var rng = new Rng(seed ^ 0xABCDEF);
        var lines = new List<string>();
        string[] names = ["df1-10", "df100-300", "df2000+"];
        Func<long, bool>[] pred = [d => d is >= 1 and <= 10, d => d is >= 100 and <= 300, d => d >= 2000];
        for (var b = 0; b < 3; b++)
        {
            var pool = terms.Where(t => pred[b](t.Doc)).ToList();
            for (var i = 0; i < 10 && pool.Count > 0; i++)
            {
                var pick = pool[rng.NextInt(pool.Count)];
                lines.Add(names[b] + "\t" + pick.Term);
            }
        }

        var rowCount = Convert.ToInt32(Db.Scalar(c, $"SELECT max(id) FROM {table}"), CultureInfo.InvariantCulture);
        for (var i = 0; i < 10; i++)
        {
            var text = Convert.ToString(Db.Scalar(c, $"SELECT {column} FROM {table} WHERE id = {1 + rng.NextInt(rowCount)}"), CultureInfo.InvariantCulture) ?? "";
            var words = text.Split(' ').Where(w => w.Length >= 3 && w.All(char.IsLetter)).Distinct().ToArray();
            if (words.Length >= 2)
            {
                var first = rng.NextInt(words.Length);
                var second = (first + 1 + rng.NextInt(words.Length - 1)) % words.Length;
                lines.Add("and2\t" + words[first] + " " + words[second]);
            }
        }

        File.WriteAllLines(file, lines);
        Out.Kv(o, "queries", ("index", fts), ("df1-10_candidates", terms.Count(t => t.Doc <= 10)), ("df100-300_candidates", terms.Count(t => t.Doc is >= 100 and <= 300)), ("df2000+_candidates", terms.Count(t => t.Doc >= 2000)), ("terms", terms.Count), ("lines", lines.Count));
        Db.Exec(c, $"DROP TABLE temp.{fts}_v");
    }
}
