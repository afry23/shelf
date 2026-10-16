using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace Proto;

/// <summary>Percentiles with the nearest-rank rule (the same rule as measure.sh).</summary>
public static class Stats
{
    public static double Median(double[] values)
    {
        var s = (double[])values.Clone();
        Array.Sort(s);
        return s.Length % 2 == 1 ? s[s.Length / 2] : (s[(s.Length / 2) - 1] + s[s.Length / 2]) / 2;
    }

    public static double Percentile(double[] values, double p)
    {
        var s = (double[])values.Clone();
        Array.Sort(s);
        var rank = (int)Math.Ceiling(p * s.Length);
        return s[Math.Clamp(rank, 1, s.Length) - 1];
    }

    public static string Summary(double[] values) =>
        FormattableString.Invariant($"p50={Median(values):F3} p95={Percentile(values, 0.95):F3} max={values.Max():F3} n={values.Length}");
}

/// <summary>xoshiro256** seeded through SplitMix64.</summary>
public sealed class Rng
{
    private ulong _s0, _s1, _s2, _s3;

    public Rng(ulong seed)
    {
        _s0 = SplitMix(ref seed);
        _s1 = SplitMix(ref seed);
        _s2 = SplitMix(ref seed);
        _s3 = SplitMix(ref seed);
    }

    private static ulong SplitMix(ref ulong x)
    {
        x += 0x9E3779B97F4A7C15UL;
        var z = x;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    private static ulong Rotl(ulong x, int k) => (x << k) | (x >> (64 - k));

    public ulong NextULong()
    {
        var result = Rotl(_s1 * 5, 7) * 9;
        var t = _s1 << 17;
        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= t;
        _s3 = Rotl(_s3, 45);
        return result;
    }

    /// <summary>Uniform in (0, 1].</summary>
    public double NextDouble() => ((NextULong() >> 11) + 1) * (1.0 / 9007199254740992.0);

    public int NextInt(int exclusiveMax) => (int)(NextULong() % (ulong)exclusiveMax);

    /// <summary>Standard normal via Box-Muller (one value per call pair, second value cached).</summary>
    private double? _spare;

    public double NextGaussian()
    {
        if (_spare is { } s)
        {
            _spare = null;
            return s;
        }

        var u1 = NextDouble();
        var u2 = NextDouble();
        var r = Math.Sqrt(-2.0 * Math.Log(u1));
        _spare = r * Math.Sin(2 * Math.PI * u2);
        return r * Math.Cos(2 * Math.PI * u2);
    }
}

public static class Vectors
{
    /// <summary>Fills dst with an L2-normalized Gaussian float32 vector.</summary>
    public static void FillUnit(Rng rng, Span<float> dst)
    {
        double sum = 0;
        var tmp = new double[dst.Length];
        for (var i = 0; i < dst.Length; i++)
        {
            tmp[i] = rng.NextGaussian();
            sum += tmp[i] * tmp[i];
        }

        var inv = 1.0 / Math.Sqrt(sum);
        for (var i = 0; i < dst.Length; i++)
        {
            dst[i] = (float)(tmp[i] * inv);
        }
    }

    public static ReadOnlySpan<byte> ToBytes(ReadOnlySpan<float> v)
    {
        if (!BitConverter.IsLittleEndian)
        {
            throw new PlatformNotSupportedException("big-endian host");
        }

        return MemoryMarshal.AsBytes(v);
    }

    public static float[] FromBytes(ReadOnlySpan<byte> b)
    {
        var f = new float[b.Length / 4];
        b.CopyTo(MemoryMarshal.AsBytes(f.AsSpan()));
        return f;
    }

    public static double DotRef(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        double s = 0;
        for (var i = 0; i < a.Length; i++)
        {
            s += (double)a[i] * b[i];
        }

        return s;
    }
}

public static class Out
{
    /// <summary>One result line "key=value key=value" with invariant formatting.</summary>
    public static void Kv(TextWriter w, string name, params (string Key, object Value)[] pairs)
    {
        var sb = new StringBuilder(name);
        foreach (var (k, v) in pairs)
        {
            sb.Append(' ').Append(k).Append('=').Append(Convert.ToString(v, CultureInfo.InvariantCulture));
        }

        w.WriteLine(sb.ToString());
    }
}

/// <summary>Self-checks of the helpers; the commands fail when one is broken.</summary>
public static class SelfCheck
{
    public static int Run(TextWriter w)
    {
        var failures = 0;
        void Check(string name, bool ok)
        {
            w.WriteLine(ok ? $"pass selfcheck:{name}" : $"fail selfcheck:{name}");
            if (!ok)
            {
                failures++;
            }
        }

        double[] ten = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
        Check("median-even", Stats.Median(ten) == 5.5 && Stats.Percentile(ten, 0.95) == 10);
        Check("median-odd", Stats.Median([3, 1, 2]) == 2);

        float[] f = [1f, -2.5f];
        var bytes = Vectors.ToBytes(f).ToArray();
        byte[] expected = [0x00, 0x00, 0x80, 0x3F, 0x00, 0x00, 0x20, 0xC0];
        Check("blob-bytes", bytes.AsSpan().SequenceEqual(expected) && BitConverter.IsLittleEndian);
        Check("blob-roundtrip", Vectors.FromBytes(bytes).AsSpan().SequenceEqual(f));

        var a = new float[384];
        var b = new float[384];
        Vectors.FillUnit(new Rng(7), a);
        Vectors.FillUnit(new Rng(7), b);
        Check("vector-seed", Vectors.ToBytes(a).SequenceEqual(Vectors.ToBytes(b)));
        var rng = new Rng(11);
        var normOk = true;
        for (var i = 0; i < 100; i++)
        {
            Vectors.FillUnit(rng, a);
            normOk &= Math.Abs(Math.Sqrt(Vectors.DotRef(a, a)) - 1) < 1e-5;
        }

        Check("vector-norm", normOk);
        return failures;
    }
}

public sealed class Clock
{
    private readonly long _start = Stopwatch.GetTimestamp();

    public double Ms => Stopwatch.GetElapsedTime(_start).TotalMilliseconds;

    public static double Time(Action a)
    {
        var s = Stopwatch.GetTimestamp();
        a();
        return Stopwatch.GetElapsedTime(s).TotalMilliseconds;
    }
}
