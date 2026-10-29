using System.CommandLine;
using Shelf.Core.Organization;
using Shelf.Index;

namespace Shelf.Cli.Commands;

/// <summary><c>shelf organize [--apply] [--pattern ...]</c> (F-ORG-5): a dry run by default, moves only with <c>--apply</c>.</summary>
internal static class OrganizeCommand
{
    public static Command Create(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var apply = new Option<bool>("--apply") { Description = "Move the files (without it, only show what would move)" };
        var pattern = new Option<string?>("--pattern")
        {
            Description = "Path pattern for books instead of organize.pattern, e.g. '{author_sort}/{title} ({year})/{title}.{ext}'",
        };
        var paperPattern = new Option<string?>("--paper-pattern")
        {
            Description = "Path pattern for papers instead of organize.paper_pattern, e.g. 'papers/{year}/{cite_key}/{cite_key}.{ext}'; --pattern is for books",
        };
        var library = LibraryContext.CreateOption();
        var command = new Command("organize", "Move the files to the folders and names of the pattern (dry run unless --apply)");
        command.Options.Add(apply);
        command.Options.Add(pattern);
        command.Options.Add(paperPattern);
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            PathPattern? chosen = null;
            if (parseResult.GetValue(pattern) is { } text)
            {
                try
                {
                    chosen = PathPattern.Parse(text);
                }
                catch (PatternException e)
                {
                    stderr.WriteLine($"error: --pattern: {e.Message}");
                    return ExitCodes.Usage;
                }
            }

            PathPattern? chosenPaper = null;
            if (parseResult.GetValue(paperPattern) is { } paperText)
            {
                try
                {
                    chosenPaper = PathPattern.Parse(paperText);
                }
                catch (PatternException e)
                {
                    stderr.WriteLine($"error: --paper-pattern: {e.Message}");
                    return ExitCodes.Usage;
                }
            }

            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            var doApply = parseResult.GetValue(apply);
            var result = LibraryOrganizer.Organize(context.Root, chosen ?? context.Settings.Organize.Pattern, doApply, chosenPaper ?? context.Settings.Organize.PaperPattern);
            foreach (var collision in result.Collisions)
            {
                stderr.WriteLine($"warning: skipped {collision.From} -> {collision.To}: {collision.Reason}");
            }

            var useJson = parseResult.GetValue(json);
            if (useJson)
            {
                stdout.WriteLine(JsonOutput.Write(writer => WriteJson(writer, result)));
            }
            else
            {
                WriteText(stdout, Palette.For(environment, json: false), result);
            }

            return ExitCodes.Success;
        });
        return command;
    }

    private static void WriteText(TextWriter stdout, Palette palette, OrganizeResult result)
    {
        foreach (var change in result.Changes)
        {
            stdout.WriteLine($"{palette.Dim(change.From)} -> {palette.Bold(change.To)}");
        }

        if (result.Changes.Count > 0)
        {
            stdout.WriteLine();
        }

        var notes = new List<string>();
        if (result.Unchanged > 0 && result.Files == 0)
        {
            notes.Add($"{TextFormat.Plural(result.Unchanged, "file", "files")} already in place");
        }

        if (result.Collisions.Count > 0)
        {
            notes.Add(TextFormat.Plural(result.Collisions.Count, "collision", "collisions"));
        }

        var suffix = notes.Count > 0 ? $" ({string.Join(", ", notes)})" : string.Empty;
        if (result.Files == 0)
        {
            stdout.WriteLine($"Nothing to organize{suffix}");
            return;
        }

        var amount = $"{TextFormat.Plural(result.Files, "file", "files")} in {TextFormat.Plural(result.Works, "work", "works")}";
        stdout.WriteLine(result.Applied
            ? $"Moved {amount}{suffix}."
            : $"Would move {amount}{suffix}. Run with --apply to do it.");
    }

    private static void WriteJson(System.Text.Json.Utf8JsonWriter writer, OrganizeResult result)
    {
        writer.WriteStartObject();
        JsonOutput.Property(writer, "apply", result.Applied);
        JsonOutput.Property(writer, "files", result.Files);
        JsonOutput.Property(writer, "works", result.Works);
        JsonOutput.Property(writer, "unchanged", result.Unchanged);
        JsonOutput.Property(writer, "collisions", result.Collisions.Count);
        writer.WriteStartArray("changes");
        foreach (var change in result.Changes)
        {
            writer.WriteStartObject();
            JsonOutput.Property(writer, "work", change.Work.ToString());
            JsonOutput.Property(writer, "from", change.From);
            JsonOutput.Property(writer, "to", change.To);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteStartArray("collided");
        foreach (var collision in result.Collisions)
        {
            writer.WriteStartObject();
            JsonOutput.Property(writer, "work", collision.Work.ToString());
            JsonOutput.Property(writer, "from", collision.From);
            JsonOutput.Property(writer, "to", collision.To);
            JsonOutput.Property(writer, "reason", collision.Reason);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
