using System.CommandLine;
using System.Globalization;
using Shelf.Index;

namespace Shelf.Cli.Commands;

/// <summary><c>shelf scan</c>: brings the index in line with the folders, looking only at what changed (F-LIB-7).</summary>
internal static class ScanCommand
{
    public static Command Create(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var library = LibraryContext.CreateOption();
        var command = new Command("scan", "Update the index with the changes in the library folders");
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            var result = LibraryScanner.Scan(context.Root);

            foreach (var problem in result.Problems)
            {
                stderr.WriteLine($"warning: {problem.Message}");
            }

            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonOutput.Object(
                    ("works", result.Works),
                    ("files", result.Files),
                    ("newWorks", result.NewWorks),
                    ("updatedWorks", result.UpdatedWorks),
                    ("removedWorks", result.RemovedWorks),
                    ("newFiles", result.NewFiles),
                    ("changedFiles", result.ChangedFiles),
                    ("touchedFiles", result.TouchedFiles),
                    ("removedFiles", result.RemovedFiles),
                    ("unchangedFiles", result.UnchangedFiles),
                    ("untracked", result.Untracked.Count),
                    ("problems", result.Problems.Count)));
            }
            else
            {
                foreach (var path in result.Untracked)
                {
                    stdout.WriteLine($"untracked {path}  (not in a work folder: use 'shelf import --move' on its folder)");
                }

                stdout.WriteLine(Summary(result));
            }

            return result.Problems.Count == 0 ? ExitCodes.Success : ExitCodes.Error;
        });
        return command;
    }

    private static string Summary(ScanResult r)
    {
        var text = $"Scanned {Plural(r.Files, "file")} in {Plural(r.Works, "work")}: {r.NewFiles} new, {r.ChangedFiles} changed, {r.RemovedFiles} removed, {r.UnchangedFiles} unchanged";
        if (r.TouchedFiles > 0)
        {
            text += $", {r.TouchedFiles} touched";
        }

        if (r.NewWorks > 0)
        {
            text += $", {Plural(r.NewWorks, "work")} added";
        }

        if (r.UpdatedWorks > 0)
        {
            text += $", {Plural(r.UpdatedWorks, "work")} updated";
        }

        if (r.RemovedWorks > 0)
        {
            text += $", {Plural(r.RemovedWorks, "work")} removed";
        }

        if (r.Untracked.Count > 0)
        {
            text += $", {r.Untracked.Count} untracked";
        }

        return text;
    }

    private static string Plural(int count, string noun) => $"{count.ToString(CultureInfo.InvariantCulture)} {noun}{(count == 1 ? string.Empty : "s")}";
}
