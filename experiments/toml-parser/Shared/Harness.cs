using System.Globalization;
using System.Text;

namespace Proto;

/// <summary>A parse error as reported by the library under test; Line/Column are 1-based when known.</summary>
public sealed class ParseFailure(int? line, int? column, string message) : Exception(message)
{
    public int? Line { get; } = line;
    public int? Column { get; } = column;
}

/// <summary>A value looked up by dotted key. Kind is one of string, integer, float, boolean, datetime, array, table.</summary>
public readonly record struct Val(string Kind, string Text);

public interface IAdapter
{
    /// <summary>Parses text; throws <see cref="ParseFailure"/> on invalid TOML.</summary>
    object Parse(string text);

    /// <summary>Looks up a dotted key; null when absent.</summary>
    Val? Get(object document, string dottedKey);

    /// <summary>
    /// Writes the unchanged re-serialization to sameFile and the edited one (grouping.auto_merge = 0.85, new key
    /// grouping.new_key = "x") to editFile. Returns false if the library has no write path.
    /// </summary>
    bool RoundTrip(string text, string sameFile, string editFile);
}

public static class Harness
{
    public static string Float(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    public static int Run(IAdapter adapter, string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 3 && args[0] == "get")
        {
            return Get(adapter, args[1], args[2], stdout, stderr);
        }

        if (args.Length == 2 && args[0] == "corpus")
        {
            return Corpus(adapter, args[1], stdout);
        }

        if (args.Length == 3 && args[0] == "roundtrip")
        {
            return RoundTrip(adapter, args[1], args[2], stdout, stderr);
        }

        stderr.WriteLine("usage: get <file> <key> | corpus <dir> | roundtrip <in> <outPrefix>");
        return 2;
    }

    private static string Read(string path) =>
        new UTF8Encoding(false, true).GetString(File.ReadAllBytes(path));

    private static string Describe(string file, ParseFailure f) =>
        f.Line is null ? $"{file}: {f.Message}"
        : f.Column is null ? $"{file}:{f.Line}: {f.Message}"
        : $"{file}:{f.Line}:{f.Column}: {f.Message}";

    private static int Get(IAdapter adapter, string file, string key, TextWriter stdout, TextWriter stderr)
    {
        try
        {
            var document = adapter.Parse(Read(file));
            var value = adapter.Get(document, key);
            stdout.WriteLine(value is { } v ? $"{v.Kind}:{v.Text}" : "absent");
            return 0;
        }
        catch (ParseFailure f)
        {
            stderr.WriteLine(Describe(file, f));
            return 1;
        }
    }

    private static int Corpus(IAdapter adapter, string dir, TextWriter stdout)
    {
        var total = 0;
        var passed = 0;
        var lineChecked = 0;
        var lineCorrect = 0;

        foreach (var file in Directory.GetFiles(Path.Combine(dir, "valid"), "*.toml").Order(StringComparer.Ordinal))
        {
            total++;
            var name = "valid/" + Path.GetFileName(file);
            try
            {
                var document = adapter.Parse(Read(file));
                var failures = new List<string>();
                var expectedFile = Path.ChangeExtension(file, ".expected");
                foreach (var raw in File.ReadAllLines(expectedFile))
                {
                    if (raw.Length == 0)
                    {
                        continue;
                    }

                    var separator = raw.IndexOf(" = ", StringComparison.Ordinal);
                    var key = raw[..separator];
                    var expected = raw[(separator + 3)..];
                    var actual = adapter.Get(document, key) is { } v ? $"{v.Kind}:{v.Text}" : "absent";
                    // "kind:*" accepts any text of that kind (datetime text formats differ between libraries).
                    var matches = expected.EndsWith(":*", StringComparison.Ordinal)
                        ? actual.StartsWith(expected[..^1], StringComparison.Ordinal)
                        : string.Equals(actual, expected, StringComparison.Ordinal);
                    if (!matches)
                    {
                        failures.Add($"{key}: expected '{expected}', got '{actual}'");
                    }
                }

                if (failures.Count == 0)
                {
                    passed++;
                    stdout.WriteLine($"pass {name}");
                }
                else
                {
                    stdout.WriteLine($"fail {name} ({string.Join("; ", failures)})");
                }
            }
            catch (ParseFailure f)
            {
                stdout.WriteLine($"fail {name} (rejected: {f.Message.ReplaceLineEndings(" ")})");
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                stdout.WriteLine($"fail {name} (crash {e.GetType().Name}: {e.Message.ReplaceLineEndings(" ")})");
            }
        }

        foreach (var file in Directory.GetFiles(Path.Combine(dir, "invalid"), "*.toml").Order(StringComparer.Ordinal))
        {
            total++;
            var name = "invalid/" + Path.GetFileName(file);
            try
            {
                adapter.Parse(Read(file));
                stdout.WriteLine($"fail {name} (accepted)");
            }
            catch (ParseFailure f)
            {
                passed++;
                var lineFile = Path.ChangeExtension(file, ".line");
                var note = $"line={f.Line?.ToString(CultureInfo.InvariantCulture) ?? "none"} col={f.Column?.ToString(CultureInfo.InvariantCulture) ?? "none"}";
                if (File.Exists(lineFile))
                {
                    lineChecked++;
                    var want = int.Parse(File.ReadAllText(lineFile).Trim(), CultureInfo.InvariantCulture);
                    if (f.Line == want)
                    {
                        lineCorrect++;
                        note += " line-ok";
                    }
                    else
                    {
                        note += $" line-WRONG(want {want})";
                    }
                }

                stdout.WriteLine($"pass {name} {note}");
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                stdout.WriteLine($"fail {name} (crash {e.GetType().Name}: {e.Message.ReplaceLineEndings(" ")})");
            }
        }

        stdout.WriteLine($"passed {passed}/{total}");
        stdout.WriteLine($"lines {lineCorrect}/{lineChecked}");
        return 0;
    }

    private static int RoundTrip(IAdapter adapter, string input, string outPrefix, TextWriter stdout, TextWriter stderr)
    {
        try
        {
            var supported = adapter.RoundTrip(Read(input), outPrefix + ".same.toml", outPrefix + ".edit.toml");
            stdout.WriteLine(supported ? "ok" : "unsupported");
            return 0;
        }
        catch (ParseFailure f)
        {
            stderr.WriteLine(Describe(input, f));
            return 1;
        }
    }
}
