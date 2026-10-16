using System.IO.MemoryMappedFiles;
using System.Numerics;
using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using Microsoft.Data.Sqlite;

namespace Proto;

public readonly record struct Hit(long Id, double Distance);

/// <summary>Brute-force cosine on unit vectors (dot product), top-k by min-heap.</summary>
public static unsafe class Bf
{
    private sealed class Heap(int k)
    {
        private readonly float[] _s = new float[k];
        private readonly int[] _ix = new int[k];
        private int _n;

        public void Offer(float score, int idx)
        {
            if (_n < k)
            {
                _s[_n] = score;
                _ix[_n] = idx;
                Up(_n++);
            }
            else if (score > _s[0])
            {
                _s[0] = score;
                _ix[0] = idx;
                Down(0);
            }
        }

        private void Swap(int a, int b)
        {
            (_s[a], _s[b]) = (_s[b], _s[a]);
            (_ix[a], _ix[b]) = (_ix[b], _ix[a]);
        }

        private void Up(int i)
        {
            while (i > 0 && _s[(i - 1) / 2] > _s[i])
            {
                Swap(i, (i - 1) / 2);
                i = (i - 1) / 2;
            }
        }

        private void Down(int i)
        {
            while (true)
            {
                var l = (2 * i) + 1;
                var r = l + 1;
                var m = i;
                if (l < _n && _s[l] < _s[m])
                {
                    m = l;
                }

                if (r < _n && _s[r] < _s[m])
                {
                    m = r;
                }

                if (m == i)
                {
                    return;
                }

                Swap(i, m);
                i = m;
            }
        }

        public IEnumerable<(int Idx, float Score)> Items() => Enumerable.Range(0, _n).Select(i => (_ix[i], _s[i]));
    }

    public static float DotManual(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        var i = 0;
        var sum = 0f;
        ref var ra = ref MemoryMarshal.GetReference(a);
        ref var rb = ref MemoryMarshal.GetReference(b);
        if (Vector256.IsHardwareAccelerated && a.Length >= 32)
        {
            var a0 = Vector256<float>.Zero;
            var a1 = Vector256<float>.Zero;
            var a2 = Vector256<float>.Zero;
            var a3 = Vector256<float>.Zero;
            for (; i <= a.Length - 32; i += 32)
            {
                a0 += Vector256.LoadUnsafe(ref ra, (nuint)i) * Vector256.LoadUnsafe(ref rb, (nuint)i);
                a1 += Vector256.LoadUnsafe(ref ra, (nuint)(i + 8)) * Vector256.LoadUnsafe(ref rb, (nuint)(i + 8));
                a2 += Vector256.LoadUnsafe(ref ra, (nuint)(i + 16)) * Vector256.LoadUnsafe(ref rb, (nuint)(i + 16));
                a3 += Vector256.LoadUnsafe(ref ra, (nuint)(i + 24)) * Vector256.LoadUnsafe(ref rb, (nuint)(i + 24));
            }

            sum = Vector256.Sum(a0 + a1 + a2 + a3);
        }
        else if (Vector128.IsHardwareAccelerated && a.Length >= 16)
        {
            var a0 = Vector128<float>.Zero;
            var a1 = Vector128<float>.Zero;
            var a2 = Vector128<float>.Zero;
            var a3 = Vector128<float>.Zero;
            for (; i <= a.Length - 16; i += 16)
            {
                a0 += Vector128.LoadUnsafe(ref ra, (nuint)i) * Vector128.LoadUnsafe(ref rb, (nuint)i);
                a1 += Vector128.LoadUnsafe(ref ra, (nuint)(i + 4)) * Vector128.LoadUnsafe(ref rb, (nuint)(i + 4));
                a2 += Vector128.LoadUnsafe(ref ra, (nuint)(i + 8)) * Vector128.LoadUnsafe(ref rb, (nuint)(i + 8));
                a3 += Vector128.LoadUnsafe(ref ra, (nuint)(i + 12)) * Vector128.LoadUnsafe(ref rb, (nuint)(i + 12));
            }

            sum = Vector128.Sum(a0 + a1 + a2 + a3);
        }

        for (; i < a.Length; i++)
        {
            sum += a[i] * b[i];
        }

        return sum;
    }

    /// <summary>Scans count rows of dim floats at base (ids = row index + 1 unless ids given); returns top-k by distance 1 - dot.</summary>
    public static Hit[] Scan(nint basePtr, int count, int dim, ReadOnlySpan<float> query, int k, bool manual, int threads, long[]? ids)
    {
        var q = query.ToArray();
        Heap Part(int from, int to)
        {
            var heap = new Heap(k);
            var qs = new ReadOnlySpan<float>(q);
            for (var r = from; r < to; r++)
            {
                var row = new ReadOnlySpan<float>((float*)basePtr + ((long)r * dim), dim);
                var s = manual ? DotManual(row, qs) : TensorPrimitives.Dot(row, qs);
                heap.Offer(s, r);
            }

            return heap;
        }

        var merged = new Heap(k);
        if (threads <= 1)
        {
            foreach (var (idx, score) in Part(0, count).Items())
            {
                merged.Offer(score, idx);
            }
        }
        else
        {
            var parts = new Heap[threads];
            var size = (count + threads - 1) / threads;
            Parallel.For(0, threads, p => parts[p] = Part(Math.Min(p * size, count), Math.Min((p + 1) * size, count)));
            foreach (var h in parts)
            {
                foreach (var (idx, score) in h.Items())
                {
                    merged.Offer(score, idx);
                }
            }
        }

        return merged.Items()
            .OrderByDescending(x => x.Score).ThenBy(x => x.Idx)
            .Select(x => new Hit(ids?[x.Idx] ?? (x.Idx + 1), 1.0 - x.Score)).ToArray();
    }

    public static Hit[] ScanArray(float[] data, int count, int dim, ReadOnlySpan<float> query, int k, bool manual, int threads, long[]? ids)
    {
        fixed (float* p = data)
        {
            return Scan((nint)p, count, dim, query, k, manual, threads, ids);
        }
    }

    /// <summary>Scalar double-precision reference (full sort).</summary>
    public static Hit[] Reference(float[] data, int count, int dim, ReadOnlySpan<float> query, int k, long[]? ids)
    {
        var scores = new double[count];
        for (var r = 0; r < count; r++)
        {
            scores[r] = Vectors.DotRef(data.AsSpan(r * dim, dim), query);
        }

        return Enumerable.Range(0, count).OrderByDescending(r => scores[r]).ThenBy(r => r).Take(k)
            .Select(r => new Hit(ids?[r] ?? (r + 1), 1.0 - scores[r])).ToArray();
    }

    public static bool Same(Hit[] a, Hit[] b, out string detail)
    {
        detail = "";
        if (a.Length != b.Length)
        {
            detail = $"length {a.Length} vs {b.Length}";
            return false;
        }

        for (var i = 0; i < a.Length; i++)
        {
            if (a[i].Id != b[i].Id || Math.Abs(a[i].Distance - b[i].Distance) >= 1e-5)
            {
                detail = $"rank {i}: {a[i]} vs {b[i]}";
                return false;
            }
        }

        return true;
    }

    public static int SelfCheck(TextWriter o)
    {
        var failures = 0;
        var rng = new Rng(99);
        const int dim = 384;
        var maxDiff = 0.0;
        var a = new float[dim];
        var b = new float[dim];
        for (var i = 0; i < 1000; i++)
        {
            Vectors.FillUnit(rng, a);
            Vectors.FillUnit(rng, b);
            var r = Vectors.DotRef(a, b);
            maxDiff = Math.Max(maxDiff, Math.Abs(TensorPrimitives.Dot(a, b) - r));
            maxDiff = Math.Max(maxDiff, Math.Abs(DotManual(a, b) - r));
        }

        var ok = maxDiff < 1e-5;
        o.WriteLine((ok ? "pass" : "fail") + " selfcheck:dot-variants-agree maxdiff=" + maxDiff.ToString("E2", System.Globalization.CultureInfo.InvariantCulture));
        failures += ok ? 0 : 1;

        var data = new float[1000 * dim];
        for (var i = 0; i < 1000; i++)
        {
            Vectors.FillUnit(rng, data.AsSpan(i * dim, dim));
        }

        Vectors.FillUnit(rng, a);
        var heap = ScanArray(data, 1000, dim, a, 10, false, 1, null);
        var reference = Reference(data, 1000, dim, a, 10, null);
        var okHeap = Same(heap, reference, out var detail) && Same(ScanArray(data, 1000, dim, a, 10, true, 4, null), reference, out detail);
        o.WriteLine((okHeap ? "pass" : "fail") + " selfcheck:heap-topk-equals-full-sort " + detail);
        failures += okHeap ? 0 : 1;
        return failures;
    }
}

/// <summary>Vector data sets in SQLite (BLOB tables), flat files and sqlite-vec tables.</summary>
public static class VecData
{
    public const int FlatHeader = 16;

    public static string IdColumn(string table) => table[..^1] + "_id";

    public static float[] Query(int index, int dim)
    {
        var v = new float[dim];
        Vectors.FillUnit(new Rng((ulong)(1_000_000 + index)), v);
        return v;
    }

    public static int Gen(Args a, TextWriter o)
    {
        var db = a.Positional[0];
        var table = a.Str("table");
        var count = a.Int("count", 0);
        var dim = a.Int("dim", 384);
        var rng = new Rng((ulong)a.Int("seed", 42));
        var clock = new Clock();
        using var c = Db.Open(db);
        Db.Exec(c, $"DROP TABLE IF EXISTS emb_{table}; CREATE TABLE emb_{table}(id INTEGER PRIMARY KEY, embedding BLOB NOT NULL)");
        FileStream? flat = null;
        if (a.Has("flat"))
        {
            flat = File.Create(a.Str("flat"));
            Span<byte> head = stackalloc byte[FlatHeader];
            "VEC1"u8.CopyTo(head);
            BitConverter.TryWriteBytes(head[4..], count);
            BitConverter.TryWriteBytes(head[8..], dim);
            flat.Write(head);
        }

        var tx = c.BeginTransaction();
        using var ins = c.CreateCommand();
        ins.CommandText = $"INSERT INTO emb_{table}(id, embedding) VALUES ($i, $e)";
        var pi = ins.Parameters.Add("$i", SqliteType.Integer);
        var pe = ins.Parameters.Add("$e", SqliteType.Blob);
        var v = new float[dim];
        for (var i = 1; i <= count; i++)
        {
            Vectors.FillUnit(rng, v);
            var bytes = Vectors.ToBytes(v).ToArray();
            pi.Value = i;
            pe.Value = bytes;
            ins.Transaction = tx;
            ins.ExecuteNonQuery();
            flat?.Write(bytes);
            if (i % 1000 == 0)
            {
                tx.Commit();
                tx = c.BeginTransaction();
            }
        }

        tx.Commit();
        flat?.Dispose();
        Db.Exec(c, "PRAGMA wal_checkpoint(TRUNCATE)");
        Out.Kv(o, "vec-gen", ("table", table), ("count", count), ("dim", dim), ("ms", clock.Ms.ToString("F0", System.Globalization.CultureInfo.InvariantCulture)), ("db_bytes", Db.FileBytes(db)));
        return 0;
    }

    public static void LoadExt(SqliteConnection c, string path)
    {
        c.EnableExtensions(true);
        c.LoadExtension(path);
    }

    public static string DefaultExt() => Path.Combine(AppContext.BaseDirectory, "vec0");

    public static int VecLoad(Args a, TextWriter o)
    {
        var db = a.Positional[0];
        var table = a.Str("table");
        var dim = a.Int("dim", 384);
        var before = Db.FileBytes(db);
        var clock = new Clock();
        using var c = Db.Open(db);
        LoadExt(c, a.Str("ext", DefaultExt()));
        Db.Exec(c, $"DROP TABLE IF EXISTS vec_{table}; CREATE VIRTUAL TABLE vec_{table} USING vec0({IdColumn(table)} INTEGER PRIMARY KEY, embedding float[{dim}] distance_metric=cosine)");
        using var sel = c.CreateCommand();
        sel.CommandText = $"SELECT id, embedding FROM emb_{table} ORDER BY id";
        using var ins = c.CreateCommand();
        ins.CommandText = $"INSERT INTO vec_{table}({IdColumn(table)}, embedding) VALUES ($i, $e)";
        var pi = ins.Parameters.Add("$i", SqliteType.Integer);
        var pe = ins.Parameters.Add("$e", SqliteType.Blob);
        var tx = c.BeginTransaction();
        ins.Transaction = tx;
        sel.Transaction = tx;
        var n = 0;
        using (var r = sel.ExecuteReader())
        {
            var buf = new byte[dim * 4];
            while (r.Read())
            {
                var got = r.GetBytes(1, 0, buf, 0, buf.Length);
                if (got != buf.Length)
                {
                    throw new InvalidOperationException("short blob");
                }

                pi.Value = r.GetInt64(0);
                pe.Value = buf;
                ins.ExecuteNonQuery();
                if (++n % 1000 == 0)
                {
                    tx.Commit();
                    tx = c.BeginTransaction();
                    ins.Transaction = tx;
                }
            }
        }

        tx.Commit();
        Db.Exec(c, "PRAGMA wal_checkpoint(TRUNCATE)");
        Out.Kv(o, "vec-load", ("table", table), ("rows", n), ("dim", dim), ("ms", clock.Ms.ToString("F0", System.Globalization.CultureInfo.InvariantCulture)), ("db_bytes_delta", Db.FileBytes(db) - before));
        return 0;
    }

    public static long Count(SqliteConnection c, string table) => Convert.ToInt64(Db.Scalar(c, $"SELECT count(*) FROM emb_{table}"), System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The realistic CLI path: read all BLOBs of emb_table into one float[].</summary>
    public static float[] LoadBlobs(SqliteConnection c, string table, int dim, out long[] ids)
    {
        var count = (int)Count(c, table);
        var data = new float[(long)count * dim];
        ids = new long[count];
        using var sel = c.CreateCommand();
        sel.CommandText = $"SELECT id, embedding FROM emb_{table} ORDER BY id";
        using var r = sel.ExecuteReader();
        var buf = new byte[dim * 4];
        var i = 0;
        while (r.Read())
        {
            r.GetBytes(1, 0, buf, 0, buf.Length);
            ids[i] = r.GetInt64(0);
            MemoryMarshal.AsBytes(data.AsSpan(i * dim, dim)).Clear();
            buf.AsSpan().CopyTo(MemoryMarshal.AsBytes(data.AsSpan(i * dim, dim)));
            i++;
        }

        return data;
    }

    public static Hit[] VecKnn(SqliteConnection c, string table, float[] query)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT {IdColumn(table)}, distance FROM vec_{table} WHERE embedding MATCH $q AND k = 10 ORDER BY distance";
        cmd.Parameters.AddWithValue("$q", Vectors.ToBytes(query).ToArray());
        using var r = cmd.ExecuteReader();
        var list = new List<Hit>();
        while (r.Read())
        {
            list.Add(new Hit(r.GetInt64(0), r.GetDouble(1)));
        }

        return list.ToArray();
    }

    private static string Fmt(double v) => v.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

    private static (int Threads, bool Manual) Variant(Args a) =>
        (a.Str("threads", "1") == "all" ? Environment.ProcessorCount : 1, a.Str("dot", "tp") == "manual");

    public static int Knn(Args a, TextWriter o)
    {
        var db = a.Positional[0];
        var mode = a.Str("mode");
        var table = a.Str("table");
        var dim = a.Int("dim", 384);
        var q = new float[dim];
        Vectors.FillUnit(new Rng((ulong)a.Int("query-seed", 1)), q);
        var (threads, manual) = Variant(a);
        var total = new Clock();
        double openMs = 0, extMs = 0, loadMs = 0, scanMs = 0;
        Hit[] hits;
        if (mode == "bf-mmap")
        {
            var clock = new Clock();
            using var mmf = MemoryMappedFile.CreateFromFile(a.Str("flat"), FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
            using var view = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
            openMs = clock.Ms;
            hits = ScanMapped(view, dim, q, manual, threads, out scanMs);
        }
        else
        {
            var clock = new Clock();
            using var c = Db.Open(db);
            openMs = clock.Ms;
            if (mode == "vec")
            {
                clock = new Clock();
                LoadExt(c, a.Str("ext", DefaultExt()));
                extMs = clock.Ms;
                clock = new Clock();
                hits = VecKnn(c, table, q);
                scanMs = clock.Ms;
            }
            else
            {
                clock = new Clock();
                var data = LoadBlobs(c, table, dim, out var ids);
                loadMs = clock.Ms;
                clock = new Clock();
                hits = Bf.ScanArray(data, ids.Length, dim, q, 10, manual, threads, ids);
                scanMs = clock.Ms;
            }
        }

        foreach (var h in hits)
        {
            o.WriteLine(FormattableString.Invariant($"{h.Id}\t{h.Distance:F6}"));
        }

        Out.Kv(o, "knn", ("mode", mode), ("table", table), ("dim", dim), ("threads", threads), ("dot", manual ? "manual" : "tp"),
            ("open_ms", Fmt(openMs)), ("ext_ms", Fmt(extMs)), ("load_ms", Fmt(loadMs)), ("scan_ms", Fmt(scanMs)), ("total_ms", Fmt(total.Ms)));
        return 0;
    }

    private static unsafe Hit[] ScanMapped(MemoryMappedViewAccessor view, int dim, float[] q, bool manual, int threads, out double scanMs)
    {
        byte* raw = null;
        view.SafeMemoryMappedViewHandle.AcquirePointer(ref raw);
        try
        {
            var count = *(int*)(raw + 4);
            if (*(int*)(raw + 8) != dim)
            {
                throw new InvalidOperationException("dimension mismatch in flat file");
            }

            var clock = new Clock();
            var hits = Bf.Scan((nint)(raw + FlatHeader), count, dim, q, 10, manual, threads, null);
            scanMs = clock.Ms;
            return hits;
        }
        finally
        {
            view.SafeMemoryMappedViewHandle.ReleasePointer();
        }
    }

    public static int KnnBench(Args a, TextWriter o)
    {
        var db = a.Positional[0];
        var mode = a.Str("mode");
        var table = a.Str("table");
        var dim = a.Int("dim", 384);
        var queries = a.Int("queries", 100);
        var (threads, manual) = Variant(a);
        var fails = Bf.SelfCheck(o);
        using var c = Db.Open(db);
        var loadClock = new Clock();
        var data = LoadBlobs(c, table, dim, out var ids);
        var loadMs = loadClock.Ms;
        var count = ids.Length;
        var lat = new List<double>();
        var verified = 0;
        string firstDiff = "";

        MemoryMappedFile? mmf = null;
        MemoryMappedViewAccessor? view = null;
        if (mode == "vec")
        {
            LoadExt(c, a.Str("ext", DefaultExt()));
        }
        else if (mode == "bf-mmap")
        {
            mmf = MemoryMappedFile.CreateFromFile(a.Str("flat"), FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
            view = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
        }

        for (var i = 0; i < queries + 3; i++)
        {
            var q = Query(i % queries, dim);
            var clock = new Clock();
            Hit[] hits;
            if (mode == "vec")
            {
                hits = VecKnn(c, table, q);
            }
            else if (mode == "bf-mmap")
            {
                hits = ScanMapped(view!, dim, q, manual, threads, out _);
            }
            else
            {
                hits = Bf.ScanArray(data, count, dim, q, 10, manual, threads, ids);
            }

            var ms = clock.Ms;
            if (i >= 3)
            {
                lat.Add(ms);
                var reference = Bf.Reference(data, count, dim, q, 10, ids);
                if (Bf.Same(hits, reference, out var detail))
                {
                    verified++;
                }
                else if (firstDiff.Length == 0)
                {
                    firstDiff = $"query {i - 3}: {detail}";
                }
            }
        }

        view?.Dispose();
        mmf?.Dispose();
        var reload = new List<double>();
        if (mode == "bf-sql")
        {
            for (var i = 0; i < 3; i++)
            {
                var clock = new Clock();
                _ = LoadBlobs(c, table, dim, out _);
                reload.Add(clock.Ms);
            }
        }

        o.WriteLine(FormattableString.Invariant($"knn-bench mode={mode} table={table} dim={dim} rows={count} threads={threads} dot={(manual ? "manual" : "tp")} {Stats.Summary(lat.ToArray())} verified={verified}/{queries} blob_load_ms={loadMs:F0}{(reload.Count > 0 ? $" reload_median_ms={Stats.Median(reload.ToArray()):F0}" : "")}"));
        if (firstDiff.Length > 0)
        {
            o.WriteLine("knn-bench first_diff " + firstDiff);
        }

        return fails + (verified == queries ? 0 : 1) > 0 ? 1 : 0;
    }
}
