using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.Globalization;
using Shelf.TestData.Planning;

namespace Shelf.TestData;

/// <summary>
/// Command line of the test data generator (slice plan 3.2). Same shape as <c>ShelfApp</c>: never touches the static
/// console. Exit codes: 0 success, 1 runtime error, 2 usage error.
/// </summary>
public static class GeneratorApp
{
    private const string Usage = "Usage: Shelf.TestData <generate|fixtures> [options]";
    private const int MaxWords = 100_000;

    public static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);

        var root = new Command("Shelf.TestData", "Deterministic test data for shelf");
        root.Options.Add(new HelpOption());
        root.SetAction(_ =>
        {
            stderr.WriteLine(Usage);
            return 2;
        });
        root.Subcommands.Add(GenerateCommand(stdout, stderr));
        root.Subcommands.Add(FixturesCommand(stdout, stderr));

        var parseResult = root.Parse(args);
        if (parseResult.Errors.Count > 0)
        {
            foreach (var error in parseResult.Errors)
            {
                stderr.WriteLine($"error: {error.Message}");
            }

            stderr.WriteLine(Usage);
            return 2;
        }

        return parseResult.Invoke(new InvocationConfiguration
        {
            Output = stdout,
            Error = stderr,
            EnableDefaultExceptionHandler = false,
        });
    }

    private static Command GenerateCommand(TextWriter stdout, TextWriter stderr)
    {
        var command = new Command("generate", "Generate a synthetic e-book library with a ground truth file");
        var output = new Option<string?>("--out") { Description = "Output directory (must be missing or empty)" };
        var preset = new Option<string?>("--preset") { Description = "Library shape: tiny (20 works, 30 files) or reference (10,000 works, 15,000 files)" };
        var works = new Option<string?>("--works") { Description = "Number of works" };
        var files = new Option<string?>("--files") { Description = "Number of files" };
        var seed = new Option<string?>("--seed") { Description = "Seed, 0..2147483647 (default 42)" };
        var wordsMin = new Option<string?>("--words-min") { Description = "Minimum words per work (default 3000)" };
        var wordsMax = new Option<string?>("--words-max") { Description = "Maximum words per work (default 8000)" };
        var deflate = new Option<bool>("--deflate") { Description = "Deflate ZIP entries (output is then not byte-stable across platforms)" };
        var plan = new Option<bool>("--plan") { Description = "Print the planned summary as JSON and write nothing" };
        foreach (var option in new Option[] { output, preset, works, files, seed, wordsMin, wordsMax, deflate, plan })
        {
            command.Options.Add(option);
        }

        command.SetAction(parseResult => Guard(stderr, () =>
        {
            var (worksCount, filesCount) = ResolveShape(parseResult.GetValue(preset), parseResult.GetValue(works), parseResult.GetValue(files));
            var seedValue = ParseInt("--seed", parseResult.GetValue(seed), 42, 0, int.MaxValue);
            var minValue = ParseInt("--words-min", parseResult.GetValue(wordsMin), 3000, 1, MaxWords);
            var maxValue = ParseInt("--words-max", parseResult.GetValue(wordsMax), 8000, 1, MaxWords);
            if (minValue > maxValue)
            {
                throw new UsageException("--words-min must not exceed --words-max");
            }

            var planned = LibraryPlanner.Plan(worksCount, filesCount);
            if (parseResult.GetValue(plan))
            {
                stdout.WriteLine(GroundTruth.Serialize(LibraryGenerator.Summarize(planned)));
                return 0;
            }

            var outDirectory = parseResult.GetValue(output)
                ?? throw new UsageException("--out is required unless --plan is given");
            EnsureEmpty(outDirectory);

            var stopwatch = Stopwatch.StartNew();
            var result = LibraryGenerator.Generate(
                new GeneratorOptions(worksCount, filesCount, seedValue, minValue, maxValue, parseResult.GetValue(deflate)),
                outDirectory);
            stopwatch.Stop();
            stdout.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"Generated {result.Works} works and {result.Files} files ({result.TotalBytes} bytes) in {outDirectory}"));
            stderr.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Elapsed: {stopwatch.Elapsed.TotalSeconds:F1} s"));
            return 0;
        }));
        return command;
    }

    private static Command FixturesCommand(TextWriter stdout, TextWriter stderr)
    {
        var command = new Command("fixtures", "Write the derived fixtures of tests/fixtures/derived");
        var output = new Option<string?>("--out") { Description = "Output directory (must be missing or empty)" };
        command.Options.Add(output);
        command.SetAction(parseResult => Guard(stderr, () =>
        {
            var outDirectory = parseResult.GetValue(output) ?? throw new UsageException("--out is required");
            EnsureEmpty(outDirectory);
            DerivedFixtures.Write(outDirectory);
            stdout.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"Wrote {DerivedFixtures.FileNames.Count} derived fixtures to {outDirectory}"));
            return 0;
        }));
        return command;
    }

    private static int Guard(TextWriter stderr, Func<int> action)
    {
        try
        {
            return action();
        }
        catch (UsageException exception)
        {
            stderr.WriteLine($"error: {exception.Message}");
            return 2;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            stderr.WriteLine($"error: {exception.Message}");
            return 1;
        }
    }

    private static (int Works, int Files) ResolveShape(string? preset, string? works, string? files)
    {
        if (preset is not null)
        {
            if (works is not null || files is not null)
            {
                throw new UsageException("--preset cannot be combined with --works or --files");
            }

            return preset switch
            {
                "tiny" => (20, 30),
                "reference" => (10_000, 15_000),
                _ => throw new UsageException($"Unknown preset '{preset}'"),
            };
        }

        if (works is null && files is null)
        {
            throw new UsageException("Either --preset or --works and --files must be given");
        }

        if (works is null || files is null)
        {
            throw new UsageException("--works and --files must be given together");
        }

        var worksValue = ParseInt("--works", works, 0, int.MinValue, int.MaxValue);
        if (worksValue < 1)
        {
            throw new UsageException("--works must be at least 1");
        }

        return (worksValue, ParseInt("--files", files, 0, int.MinValue, int.MaxValue));
    }

    private static int ParseInt(string name, string? text, int defaultValue, int min, int max)
    {
        if (text is null)
        {
            return defaultValue;
        }

        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            && !(text.StartsWith('-') && int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value)))
        {
            throw new UsageException($"{name} must be an integer");
        }

        if (value < min || value > max)
        {
            throw new UsageException($"{name} must be between {min.ToString(CultureInfo.InvariantCulture)} and {max.ToString(CultureInfo.InvariantCulture)}");
        }

        return value;
    }

    private static void EnsureEmpty(string directory)
    {
        if (File.Exists(directory))
        {
            throw new InvalidOperationException($"Output directory '{directory}' is not empty");
        }

        if (Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any())
        {
            throw new InvalidOperationException($"Output directory '{directory}' is not empty");
        }
    }
}
