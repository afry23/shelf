using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

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
        values.Length == 0
            ? "n=0"
            : FormattableString.Invariant($"p50={Median(values):F3} p95={Percentile(values, 0.95):F3} max={values.Max():F3} n={values.Length}");
}

public static class Out
{
    /// <summary>One result line "name key=value key=value" with invariant formatting.</summary>
    public static void Kv(TextWriter w, string name, params (string Key, object? Value)[] pairs)
    {
        var sb = new StringBuilder(name);
        foreach (var (k, v) in pairs)
        {
            sb.Append(' ').Append(k).Append('=').Append(Convert.ToString(v, CultureInfo.InvariantCulture));
        }

        w.WriteLine(sb.ToString());
    }
}

/// <summary>Collects check rows: pass / fail (gated) and observed (recorded only).</summary>
public sealed class Rows(TextWriter w)
{
    public int Passed { get; private set; }

    public int Total { get; private set; }

    public int Failed { get; private set; }

    public void Gate(string id, bool ok, string detail = "")
    {
        Total++;
        if (ok)
        {
            Passed++;
            w.WriteLine(detail.Length == 0 ? $"pass {id}" : $"pass {id}: {detail}");
        }
        else
        {
            Failed++;
            w.WriteLine($"fail {id}: {detail}");
        }
    }

    public void Observe(string id, string value) => w.WriteLine($"observed {id}: {value}");

    public int Finish()
    {
        w.WriteLine(FormattableString.Invariant($"passed {Passed}/{Total}"));
        return Failed == 0 ? 0 : 1;
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

/// <summary>Positional arguments plus --flag [value] options.</summary>
public sealed class Args
{
    private readonly Dictionary<string, string?> _options = new(StringComparer.Ordinal);

    public Args(string[] args, int start)
    {
        for (var i = start; i < args.Length; i++)
        {
            if (args[i].StartsWith("--", StringComparison.Ordinal))
            {
                var hasValue = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal);
                _options[args[i]] = hasValue ? args[++i] : null;
            }
            else
            {
                Positional.Add(args[i]);
            }
        }
    }

    public List<string> Positional { get; } = [];

    public bool Has(string name) => _options.ContainsKey(name);

    public string? Get(string name) => _options.GetValueOrDefault(name);

    public int GetInt(string name, int fallback) =>
        _options.TryGetValue(name, out var v) && v is not null ? int.Parse(v, CultureInfo.InvariantCulture) : fallback;
}

public static class Text
{
    /// <summary>All runs of Unicode whitespace become one space; trimmed; NFC.</summary>
    public static string Collapse(string s)
    {
        var sb = new StringBuilder(s.Length);
        var pendingSpace = false;
        foreach (var c in s)
        {
            if (char.IsWhiteSpace(c) || c == ' ')
            {
                pendingSpace = sb.Length > 0;
            }
            else
            {
                if (pendingSpace)
                {
                    sb.Append(' ');
                    pendingSpace = false;
                }

                sb.Append(c);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    public static string Sha256Hex(ReadOnlySpan<byte> data) => Convert.ToHexStringLower(SHA256.HashData(data));

    public static string Sha256Hex(string s) => Sha256Hex(Encoding.UTF8.GetBytes(s));

    /// <summary>Counts whitespace-separated words without allocating.</summary>
    public static long CountWords(string s)
    {
        long n = 0;
        var inWord = false;
        foreach (var c in s)
        {
            if (char.IsWhiteSpace(c))
            {
                inWord = false;
            }
            else if (!inWord)
            {
                inWord = true;
                n++;
            }
        }

        return n;
    }

    public static string First(string s, int n) => s.Length <= n ? s : s[..n];
}

/// <summary>Canonical JSON: ordinal-sorted keys, invariant culture, no whitespace. Values: null, bool, long, string, J, List.</summary>
public sealed class J
{
    private readonly SortedDictionary<string, object?> _map = new(StringComparer.Ordinal);

    public J Set(string key, object? value)
    {
        _map[key] = value;
        return this;
    }

    public static void Write(Utf8JsonWriter w, object? v)
    {
        switch (v)
        {
            case null: w.WriteNullValue(); break;
            case bool b: w.WriteBooleanValue(b); break;
            case int i: w.WriteNumberValue(i); break;
            case long l: w.WriteNumberValue(l); break;
            case string s: w.WriteStringValue(s); break;
            case J o:
                w.WriteStartObject();
                foreach (var (k, val) in o._map)
                {
                    w.WritePropertyName(k);
                    Write(w, val);
                }

                w.WriteEndObject();
                break;
            case List<object?> a:
                w.WriteStartArray();
                foreach (var e in a)
                {
                    Write(w, e);
                }

                w.WriteEndArray();
                break;
            default: throw new InvalidOperationException("unsupported canonical value " + v.GetType());
        }
    }

    public string ToJson()
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = false, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            Write(w, this);
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    public static List<object?> Arr(IEnumerable<string?> items) => [.. items];
}

public static partial class Procs
{
    [System.Runtime.InteropServices.LibraryImport("libc", EntryPoint = "getrusage")]
    private static partial int GetRUsage(int who, ref RUsage usage);

    // struct rusage on 64-bit macOS and Linux: two timevals (16 bytes each), then ru_maxrss (long) at offset 32 and 13 more longs.
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct RUsage
    {
        public long UtimeSec, UtimeUsec, StimeSec, StimeUsec, MaxRss, R1, R2, R3, R4, R5, R6, R7, R8, R9, R10, R11, R12, R13;
    }

    /// <summary>Peak resident set size of this process. Process.PeakWorkingSet64 returns 0 on macOS, so getrusage ru_maxrss (bytes) is used there.</summary>
    public static long PeakRssBytes()
    {
        if (!OperatingSystem.IsMacOS())
        {
            using var p = Process.GetCurrentProcess();
            return p.PeakWorkingSet64;
        }

        var u = default(RUsage);
        if (GetRUsage(0, ref u) != 0)
        {
            return 0;
        }

        return u.MaxRss;
    }

    /// <summary>Runs this executable with arguments; kills it after the timeout. Returns exit code (-1 on timeout), stdout, wall ms.</summary>
    public static (int ExitCode, string Stdout, double Ms, bool TimedOut) RunSelf(string[] args, int timeoutMs)
    {
        var exe = Environment.ProcessPath ?? throw new InvalidOperationException("no process path");
        var psi = new ProcessStartInfo(exe)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        if (string.Equals(Path.GetFileNameWithoutExtension(exe), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            // JIT run through the dotnet host: the first command-line argument is the managed entry assembly.
            psi.ArgumentList.Add(Environment.GetCommandLineArgs()[0]);
        }

        foreach (var a in args)
        {
            psi.ArgumentList.Add(a);
        }

        var clock = new Clock();
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("cannot start " + exe);
        var stdout = p.StandardOutput.ReadToEndAsync();
        _ = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(timeoutMs))
        {
            try
            {
                p.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // already gone
            }

            p.WaitForExit();
            return (-1, string.Empty, clock.Ms, true);
        }

        p.WaitForExit();
        return (p.ExitCode, stdout.Result, clock.Ms, false);
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
        Check("whitespace", Text.Collapse("a \n\tb") == "a b" && Text.Collapse("  x ") == "x");
        Check("nfc", Text.Collapse("ü") == "ü" && Text.Collapse("ü") == "ü");
        Check("words", Text.CountWords(" a  bb\nccc ") == 3);
        var j = new J().Set("b", 1L).Set("a", new J().Set("z", null).Set("y", true)).Set("c", J.Arr(["x", null]));
        Check("canonical-json", j.ToJson() == "{\"a\":{\"y\":true,\"z\":null},\"b\":1,\"c\":[\"x\",null]}");
        return failures;
    }
}
