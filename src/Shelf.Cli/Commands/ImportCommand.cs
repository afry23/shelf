using System.CommandLine;
using System.Globalization;
using Shelf.Index;

namespace Shelf.Cli.Commands;

/// <summary><c>shelf import &lt;path&gt;...</c>: copies files into the library and groups them into works (F-IMP-1).</summary>
internal static class ImportCommand
{
    public static Command Create(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var paths = new Argument<string[]>("path")
        {
            Description = "Files or folders to import; folders are searched recursively",
            Arity = ArgumentArity.ZeroOrMore,
        };
        var from = new Option<string?>("--from") { Description = "Fetch the books of a cloud folder with rclone first, e.g. rclone:gdrive:Books" };
        var move = new Option<bool>("--move") { Description = "Delete the source after the copy was verified" };
        var dryRun = new Option<bool>("--dry-run") { Description = "Show what would happen and change nothing" };
        var library = LibraryContext.CreateOption();
        var command = new Command("import", "Copy files into the library and group them into works");
        command.Arguments.Add(paths);
        command.Options.Add(from);
        command.Options.Add(move);
        command.Options.Add(dryRun);
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            var givenPaths = parseResult.GetValue(paths) ?? [];
            var remote = parseResult.GetValue(from);
            var usage = Usage(givenPaths.Length, remote, parseResult.GetValue(move));
            if (usage is not null)
            {
                stderr.WriteLine($"error: {usage}");
                return ExitCodes.Usage;
            }

            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            var isDryRun = parseResult.GetValue(dryRun);
            var sources = givenPaths.Select(p => Path.GetFullPath(p, environment.WorkingDirectory)).ToList();

            // F-IMP-8: the remote files land in a temporary folder that always disappears again.
            string? download = null;
            ImportResult result;
            try
            {
                if (remote is not null)
                {
                    var spec = remote["rclone:".Length..];
                    download = Directory.CreateTempSubdirectory("shelf-import-").FullName;
                    try
                    {
                        environment.RemoteFetcher.Fetch(spec, download, environment.Cancellation);
                    }
                    catch (RemoteFetchException e)
                    {
                        stderr.WriteLine($"error: {e.Message}");
                        return e.ToolMissing ? ExitCodes.Usage : ExitCodes.Error;
                    }

                    sources.Add(download);
                }

                result = LibraryImporter.Import(context.Root, sources, new ImportOptions(parseResult.GetValue(move), isDryRun), context.Settings.Grouping);
                if (download is not null)
                {
                    result = ShowRemote(result, download, remote!["rclone:".Length..]);
                }
            }
            finally
            {
                if (download is not null)
                {
                    Directory.Delete(download, recursive: true);
                }
            }

            if (!isDryRun)
            {
                var touched = result.Items.Where(i => i.Action != ImportAction.Error && i.WorkId is not null).Select(i => i.WorkId!.Value).Distinct();
                CoverReport.Warn(LibraryCovers.FillWorks(context.Root, touched), stderr);
            }

            var counts = Counts.Of(result);
            foreach (var item in result.Items.Where(i => i.Action == ImportAction.Error))
            {
                stderr.WriteLine($"warning: {item.Message}");
            }

            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonOutput.Write(writer => WriteJson(writer, result, counts, isDryRun)));
            }
            else
            {
                foreach (var item in result.Items)
                {
                    stdout.WriteLine(Line(item, isDryRun));
                }

                stdout.WriteLine(Summary(counts, result.Ignored, isDryRun));
            }

            return counts.Errors == 0 ? ExitCodes.Success : ExitCodes.Error;
        });
        return command;
    }

    /// <summary>Why the arguments cannot work, or null. Checked before rclone runs.</summary>
    private static string? Usage(int pathCount, string? remote, bool move)
    {
        if (remote is null)
        {
            return pathCount == 0 ? "Required argument missing: give a path to import, or --from rclone:<remote>:<path>" : null;
        }

        if (pathCount > 0)
        {
            return "give either a path or --from, not both";
        }

        if (!remote.StartsWith("rclone:", StringComparison.Ordinal))
        {
            return "--from must start with rclone:, e.g. rclone:gdrive:Books";
        }

        if (!remote["rclone:".Length..].Contains(':', StringComparison.Ordinal) || remote.EndsWith(':'))
        {
            return "--from needs a remote and a path, e.g. rclone:gdrive:Books";
        }

        return move ? "--move cannot be used with --from" : null;
    }

    /// <summary>The temporary folder means nothing to the user: name the remote folder instead.</summary>
    private static ImportResult ShowRemote(ImportResult result, string download, string remote) =>
        result with
        {
            Items = [.. result.Items.Select(i => i with
            {
                Source = i.Source.Replace(download, remote, StringComparison.Ordinal),
                Message = i.Message?.Replace(download, remote, StringComparison.Ordinal),
            })],
        };

    private sealed record Counts(int Imported, int NewWorks, int Added, int Duplicates, int Errors, int ReviewEntries)
    {
        public static Counts Of(ImportResult result)
        {
            int Count(ImportAction action) => result.Items.Count(i => i.Action == action);
            var newWorks = Count(ImportAction.NewWork);
            var added = Count(ImportAction.AddedToWork);
            return new Counts(newWorks + added, newWorks, added, Count(ImportAction.Duplicate), Count(ImportAction.Error), result.Items.Sum(i => i.ReviewEntries));
        }
    }

    private static string Line(ImportItem item, bool dryRun)
    {
        var name = Path.GetFileName(item.Source);
        var label = (item.Action, dryRun) switch
        {
            (ImportAction.NewWork, false) => "new",
            (ImportAction.NewWork, true) => "would create",
            (ImportAction.AddedToWork, false) => "added",
            (ImportAction.AddedToWork, true) => "would add",
            (ImportAction.Duplicate, _) => "duplicate",
            _ => "error",
        };
        var head = $"{label.PadRight(dryRun ? 12 : 9)} {name}";
        return item.Action switch
        {
            ImportAction.NewWork => $"{head} -> {item.Destination}" + (item.Message is null ? string.Empty : $"  ({item.Message})"),
            ImportAction.AddedToWork => $"{head} -> {item.Destination}  (to \"{item.Title}\", {item.Message})",
            // The message names the file first ("<path>: <kind>: <detail>"); the line already does.
            _ => $"{head}  ({item.Message?.Replace(item.Source + ": ", string.Empty, StringComparison.Ordinal)})",
        };
    }

    private static string Summary(Counts counts, int ignored, bool dryRun)
    {
        var parts = new List<string>();
        if (counts.NewWorks > 0)
        {
            parts.Add(Plural(counts.NewWorks, "new work"));
        }

        if (counts.Added > 0)
        {
            parts.Add(counts.Added == 1 ? "1 added to an existing work" : $"{counts.Added} added to existing works");
        }

        if (counts.Duplicates > 0)
        {
            parts.Add($"{counts.Duplicates} duplicate{(counts.Duplicates == 1 ? string.Empty : "s")} skipped");
        }

        if (counts.Errors > 0)
        {
            parts.Add(Plural(counts.Errors, "error"));
        }

        if (counts.ReviewEntries > 0)
        {
            parts.Add(counts.ReviewEntries == 1 ? "1 review entry" : $"{counts.ReviewEntries} review entries");
        }

        var text = $"{(dryRun ? "Would import" : "Imported")} {Plural(counts.Imported, "file")}" + (parts.Count > 0 ? ": " + string.Join(", ", parts) : string.Empty);
        return ignored > 0 ? $"{text}; {ignored} other {(ignored == 1 ? "file" : "files")} ignored" : text;
    }

    private static string Plural(int count, string noun) => $"{count.ToString(CultureInfo.InvariantCulture)} {noun}{(count == 1 ? string.Empty : "s")}";

    private static void WriteJson(System.Text.Json.Utf8JsonWriter writer, ImportResult result, Counts counts, bool dryRun)
    {
        writer.WriteStartObject();
        JsonOutput.Property(writer, "dryRun", dryRun);
        JsonOutput.Property(writer, "imported", counts.Imported);
        JsonOutput.Property(writer, "newWorks", counts.NewWorks);
        JsonOutput.Property(writer, "added", counts.Added);
        JsonOutput.Property(writer, "duplicates", counts.Duplicates);
        JsonOutput.Property(writer, "reviewEntries", counts.ReviewEntries);
        JsonOutput.Property(writer, "errors", counts.Errors);
        JsonOutput.Property(writer, "ignored", result.Ignored);
        writer.WriteStartArray("files");
        foreach (var item in result.Items)
        {
            writer.WriteStartObject();
            JsonOutput.Property(writer, "source", item.Source);
            JsonOutput.Property(writer, "action", item.Action switch
            {
                ImportAction.NewWork => "new-work",
                ImportAction.AddedToWork => "added",
                ImportAction.Duplicate => "duplicate",
                _ => "error",
            });
            JsonOutput.Property(writer, "destination", item.Destination);
            JsonOutput.Property(writer, "work", item.WorkId?.ToString());
            JsonOutput.Property(writer, "title", item.Title);
            if (item.Action is ImportAction.NewWork or ImportAction.AddedToWork)
            {
                JsonOutput.Property(writer, "confidence", item.Confidence);
            }

            JsonOutput.Property(writer, "message", item.Message);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
