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
        var embeddings = new Option<bool>("--embeddings")
        {
            Description = "Only make the embeddings for semantic search that are missing (resumable); the rest of the index is not touched",
        };
        var command = new Command("reindex", "Rebuild the index from the files and book.json sidecars");
        command.Options.Add(library);
        command.Options.Add(metadataOnly);
        command.Options.Add(embeddings);
        command.Validators.Add(result =>
        {
            if (result.GetValue(embeddings) && result.GetValue(metadataOnly))
            {
                result.AddError("--embeddings cannot be combined with --metadata-only");
            }
        });
        command.SetAction(parseResult =>
        {
            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            if (parseResult.GetValue(embeddings))
            {
                return EmbeddingsRunner.Run(context, parseResult.GetValue(json), stdout, stderr, environment);
            }

            var result = LibraryReindexer.Rebuild(context.Root, includeContent: !parseResult.GetValue(metadataOnly));

            // Every file was read again, so every work without a cover gets its chance (a plain scan would not give it one).
            if (!parseResult.GetValue(metadataOnly))
            {
                CoverReport.Warn(LibraryCovers.FillAll(context.Root), stderr);
            }

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
