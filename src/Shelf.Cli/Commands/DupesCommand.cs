using System.CommandLine;
using System.Text.Json;
using Shelf.Index;

namespace Shelf.Cli.Commands;

/// <summary><c>shelf dupes [--interactive]</c> (F-DUP-1, F-DUP-2, F-DUP-5): lists duplicates, or asks what to do with each group.</summary>
internal static class DupesCommand
{
    public static Command Create(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var interactive = new Option<bool>("--interactive")
        {
            Description = "Ask for every group which file stays; the others go to the trash (shelf undo reverts the session)",
        };
        var library = LibraryContext.CreateOption();
        var command = new Command("dupes", "Find duplicate files: identical ones, and different files of one work in the same format");
        command.Options.Add(interactive);
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            var useJson = parseResult.GetValue(json);
            var ask = parseResult.GetValue(interactive);
            if (ask && useJson)
            {
                stderr.WriteLine("error: --interactive cannot be combined with --json");
                return ExitCodes.Usage;
            }

            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            var groups = LibraryDuplicates.Find(context.Root);
            if (useJson)
            {
                stdout.WriteLine(JsonOutput.Write(writer => WriteJson(writer, groups)));
            }
            else if (groups.Count == 0)
            {
                stdout.WriteLine("No duplicates.");
            }
            else if (ask)
            {
                Interactive(context.Root, stdout, environment);
            }
            else
            {
                WriteList(stdout, groups);
            }

            return ExitCodes.Success;
        });
        return command;
    }

    private static void WriteList(TextWriter stdout, IReadOnlyList<DuplicateGroup> groups)
    {
        for (var i = 0; i < groups.Count; i++)
        {
            var group = groups[i];
            stdout.WriteLine($"Group {TextFormat.Number(i + 1)}: {Describe(group)}");
            for (var f = 0; f < group.Files.Count; f++)
            {
                stdout.WriteLine($"  {(f == group.Keep ? '*' : ' ')} {Line(group.Files[f], group.Kind)}");
            }
        }

        var removable = groups.Sum(g => g.Files.Count - 1);
        stdout.WriteLine();
        stdout.WriteLine($"{TextFormat.Plural(groups.Count, "group", "groups")} of duplicates ({TextFormat.Plural(removable, "file", "files")} could go to the trash). Run with --interactive to clean up; * marks the file that is suggested to stay.");
    }

    private static void Interactive(string root, TextWriter stdout, CliEnvironment environment)
    {
        var session = LibraryDuplicates.Begin(root);
        var skipped = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            while (true)
            {
                var group = LibraryDuplicates.Find(root).FirstOrDefault(g => !skipped.Contains(Key(g)));
                if (group is null)
                {
                    break;
                }

                stdout.WriteLine($"Duplicates: {Describe(group)}");
                for (var f = 0; f < group.Files.Count; f++)
                {
                    stdout.WriteLine($"  {TextFormat.Number(f + 1)}) {Line(group.Files[f], group.Kind)}{(f == group.Keep ? "  (suggested to stay)" : string.Empty)}");
                }

                var answer = Ask(group, stdout, environment);
                if (answer == "q")
                {
                    break;
                }

                if (answer == "s")
                {
                    skipped.Add(Key(group));
                }
                else if (answer == "n")
                {
                    session.MarkNotDuplicates(group);
                    stdout.WriteLine("Marked as not duplicates; they are not suggested again.");
                }
                else
                {
                    var keep = answer.Length == 0 ? group.Keep : int.Parse(answer, System.Globalization.CultureInfo.InvariantCulture) - 1;
                    session.Resolve(group, keep);
                    foreach (var file in group.Files.Where((_, i) => i != keep))
                    {
                        stdout.WriteLine($"  to the trash: {file.Path}");
                    }
                }
            }
        }
        finally
        {
            session.Finish();
        }

        stdout.WriteLine(session.Trashed == 0
            ? "Nothing moved to the trash."
            : $"Moved {TextFormat.Plural(session.Trashed, "file", "files")} to the trash. shelf undo reverts this; shelf trash empty deletes the trash for good.");
    }

    /// <returns>"q", "s", "n", "" (the suggestion) or the number of the file that stays. The end of the input is "q".</returns>
    private static string Ask(DuplicateGroup group, TextWriter stdout, CliEnvironment environment)
    {
        var count = group.Files.Count;
        while (true)
        {
            stdout.Write($"Keep which? [1-{TextFormat.Number(count)}, Enter = {TextFormat.Number(group.Keep + 1)}, s = skip, n = not duplicates, q = quit]: ");
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
            if (answer is "" or "s" or "n" or "q" || (int.TryParse(answer, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var number) && number >= 1 && number <= count))
            {
                return answer;
            }

            stdout.WriteLine($"Please answer 1-{TextFormat.Number(count)}, Enter, s, n or q.");
        }
    }

    private static string Key(DuplicateGroup group) => string.Join('|', group.Files.Select(f => f.Path));

    private static string Describe(DuplicateGroup group) => group.Kind == DuplicateKind.Identical
        ? $"identical files ({group.Format})"
        : $"same work, same format ({group.Format}), different files";

    private static string Line(DuplicateFile file, DuplicateKind kind)
    {
        var text = $"{file.Path}  {TextFormat.Size(file.Size)}";
        if (kind == DuplicateKind.SameFormat)
        {
            text += $"{(file.Pages is { } pages ? $"  {TextFormat.Plural(pages, "page", "pages")}" : string.Empty)}  metadata {file.Completeness}/{LibraryDuplicates.MetadataFields}";
        }

        return text;
    }

    private static void WriteJson(Utf8JsonWriter writer, IReadOnlyList<DuplicateGroup> groups)
    {
        writer.WriteStartObject();
        writer.WriteStartArray("groups");
        foreach (var group in groups)
        {
            writer.WriteStartObject();
            JsonOutput.Property(writer, "kind", group.Kind == DuplicateKind.Identical ? "identical" : "same-format");
            JsonOutput.Property(writer, "format", group.Format);
            writer.WriteStartArray("files");
            for (var i = 0; i < group.Files.Count; i++)
            {
                var file = group.Files[i];
                writer.WriteStartObject();
                JsonOutput.Property(writer, "work", file.Work.ToString());
                JsonOutput.Property(writer, "title", file.Title);
                JsonOutput.Property(writer, "path", file.Path);
                writer.WriteNumber("size", file.Size);
                JsonOutput.Property(writer, "pages", file.Pages);
                JsonOutput.Property(writer, "completeness", file.Completeness);
                JsonOutput.Property(writer, "keep", i == group.Keep);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
