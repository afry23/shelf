using System.CommandLine;
using Shelf.Index;

namespace Shelf.Cli.Commands;

/// <summary><c>shelf reindex</c>: rebuilds the index from the files and sidecars (F-LIB-6).</summary>
internal static class ReindexCommand
{
    public static Command Create(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var library = LibraryContext.CreateOption();
        var metadataOnly = new Option<bool>("--metadata-only")
        {
            Description = "Rebuild only the metadata of the sidecars; a later scan reads the files",
        };
        var command = new Command("reindex", "Rebuild the index from the files and book.json sidecars");
        command.Options.Add(library);
        command.Options.Add(metadataOnly);
        command.SetAction(parseResult =>
        {
            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            var result = LibraryReindexer.Rebuild(context.Root, includeContent: !parseResult.GetValue(metadataOnly));

            foreach (var problem in result.Problems)
            {
                stderr.WriteLine($"warning: {problem.Message}");
            }

            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonOutput.Object(("works", result.Works), ("files", result.Files), ("problems", result.Problems.Count)));
            }
            else
            {
                var problems = result.Problems.Count;
                var suffix = problems == 0 ? string.Empty : $", {problems} {(problems == 1 ? "problem" : "problems")}";
                stdout.WriteLine($"Indexed {result.Works} {(result.Works == 1 ? "work" : "works")} with {result.Files} {(result.Files == 1 ? "file" : "files")}{suffix}");
            }

            return result.Problems.Count == 0 ? ExitCodes.Success : ExitCodes.Error;
        });
        return command;
    }
}
