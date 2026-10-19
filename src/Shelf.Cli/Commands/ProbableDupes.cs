using System.Globalization;
using System.Text.Json;
using Shelf.Index;
using Shelf.Semantic;

namespace Shelf.Cli.Commands;

/// <summary><c>shelf dupes --probable</c> (F-DUP-3, F-DUP-6): works that probably are one book, for manual review.</summary>
internal static class ProbableDupes
{
    public static int Run(LibraryContext context, bool json, bool ask, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        if (ask && json)
        {
            stderr.WriteLine("error: --interactive cannot be combined with --json");
            return ExitCodes.Usage;
        }

        var found = ProbableDuplicates.Find(context.Root, context.Settings.Grouping);
        if (json)
        {
            stdout.WriteLine(JsonOutput.Write(writer => WriteJson(writer, found.Pairs)));
        }
        else if (found.Pairs.Count == 0)
        {
            stdout.WriteLine("No probable duplicates.");
        }
        else if (ask)
        {
            Interactive(context, found.Pairs, stdout, environment);
        }
        else
        {
            WriteList(stdout, found.Pairs);
        }

        if (found.Note is { } note)
        {
            stderr.WriteLine($"note: {note}");
        }

        return ExitCodes.Success;
    }

    private static void WriteList(TextWriter stdout, IReadOnlyList<ProbableDuplicate> pairs)
    {
        for (var i = 0; i < pairs.Count; i++)
        {
            stdout.WriteLine($"Pair {TextFormat.Number(i + 1)}: {Describe(pairs[i])}");
            stdout.WriteLine($"    {Line(pairs[i].A)}");
            stdout.WriteLine($"    {Line(pairs[i].B)}");
        }

        stdout.WriteLine();
        stdout.WriteLine($"{TextFormat.Plural(pairs.Count, "pair", "pairs")} of probable duplicates. Nothing here is removed: check them by hand; shelf dupes --probable --interactive marks pairs that are no duplicates.");
    }

    private static void Interactive(LibraryContext context, IReadOnlyList<ProbableDuplicate> pairs, TextWriter stdout, CliEnvironment environment)
    {
        var session = LibraryDuplicates.Begin(context.Root);
        var marked = 0;
        try
        {
            foreach (var pair in pairs)
            {
                stdout.WriteLine($"Probable duplicates: {Describe(pair)}");
                stdout.WriteLine($"  {Line(pair.A)}");
                stdout.WriteLine($"  {Line(pair.B)}");
                var answer = Ask(stdout, environment);
                if (answer == "q")
                {
                    break;
                }

                if (answer == "n")
                {
                    session.MarkNotDuplicates(pair);
                    marked++;
                    stdout.WriteLine("Marked as not duplicates; they are not suggested again.");
                }
            }
        }
        finally
        {
            session.Finish();
        }

        if (marked == 0)
        {
            stdout.WriteLine("Nothing marked.");
        }
    }

    /// <returns>"n", "s" or "q". The end of the input is "q".</returns>
    private static string Ask(TextWriter stdout, CliEnvironment environment)
    {
        while (true)
        {
            stdout.Write("Are these one book? [n = not duplicates, s = skip, q = quit]: ");
            var line = environment.StandardInput.ReadLine();
            if (!environment.StdoutIsTerminal)
            {
                stdout.WriteLine();
            }

            if (line is null)
            {
                return "q";
            }

            var answer = line.Trim().ToLowerInvariant();
            if (answer is "n" or "s" or "q")
            {
                return answer;
            }

            stdout.WriteLine("Please answer n, s or q.");
        }
    }

    private static string Describe(ProbableDuplicate pair)
    {
        var reasons = new List<string>();
        if (pair.Hamming is { } distance)
        {
            reasons.Add($"text distance {TextFormat.Number(distance)}");
        }

        if (pair.Cosine is { } cosine)
        {
            reasons.Add($"similarity {cosine.ToString("0.00", CultureInfo.InvariantCulture)}");
        }

        return $"probably the same book ({string.Join(", ", reasons)})";
    }

    private static string Line(ProbableWork work) =>
        $"{work.Title}{(work.Authors.Count > 0 ? $" - {string.Join(", ", work.Authors)}" : string.Empty)}  [{work.Folder}]";

    private static void WriteJson(Utf8JsonWriter writer, IReadOnlyList<ProbableDuplicate> pairs)
    {
        writer.WriteStartObject();
        writer.WriteStartArray("pairs");
        foreach (var pair in pairs)
        {
            writer.WriteStartObject();
            JsonOutput.Property(writer, "hamming", pair.Hamming);
            if (pair.Cosine is { } cosine)
            {
                writer.WriteNumber("cosine", Math.Round(cosine, 4));
            }

            WriteWork(writer, "a", pair.A);
            WriteWork(writer, "b", pair.B);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteWork(Utf8JsonWriter writer, string name, ProbableWork work)
    {
        writer.WriteStartObject(name);
        JsonOutput.Property(writer, "work", work.Id.ToString());
        JsonOutput.Property(writer, "title", work.Title);
        writer.WriteStartArray("authors");
        foreach (var author in work.Authors)
        {
            writer.WriteStringValue(author);
        }

        writer.WriteEndArray();
        JsonOutput.Property(writer, "folder", work.Folder);
        writer.WriteEndObject();
    }
}
