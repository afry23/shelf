using System.CommandLine;
using Shelf.Core.Model;
using Shelf.Index;

namespace Shelf.Cli.Commands;

/// <summary><c>shelf fetch &lt;doi or arXiv ID&gt;</c> (F-PAP-8): a paper from its metadata and open-access PDF on the internet.</summary>
internal static class FetchCommand
{
    public static Command Create(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var identifier = new Argument<string>("paper")
        {
            Description = "DOI (10.1145/..., doi:..., https://doi.org/...) or arXiv ID (1706.03762, arXiv:..., https://arxiv.org/abs/...)",
        };
        var library = LibraryContext.CreateOption();
        var command = new Command("fetch", "Load a paper by DOI or arXiv ID: metadata and open-access PDF, as a new work");
        command.Arguments.Add(identifier);
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            var text = parseResult.GetRequiredValue(identifier);
            if (PaperIds.Parse(text) is not { } reference)
            {
                stderr.WriteLine($"error: '{text}' is neither a DOI nor an arXiv ID");
                return ExitCodes.Usage;
            }

            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            FetchResult result;
            try
            {
                result = LibraryPapers.Fetch(context.Root, reference, environment.MetadataSources, context.Settings.Fetch, environment.Cancellation);
            }
            catch (LibraryEditException e)
            {
                stderr.WriteLine($"error: {e.Message}");
                return ExitCodes.Error;
            }

            foreach (var warning in result.Warnings)
            {
                stderr.WriteLine($"warning: {warning}");
            }

            CoverReport.Warn(LibraryCovers.FillWorks(context.Root, [result.Work]), stderr);
            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonOutput.Write(writer =>
                {
                    writer.WriteStartObject();
                    JsonOutput.Property(writer, "id", result.Work.ToString());
                    JsonOutput.Property(writer, "title", result.Title);
                    JsonOutput.Property(writer, "file", result.File);
                    JsonOutput.Property(writer, "folder", result.Folder);
                    JsonOutput.Property(writer, "source", result.Source);
                    writer.WriteEndObject();
                }));
            }
            else
            {
                stdout.WriteLine($"Fetched {result.Title} as {result.Work} from {result.Source}");
            }

            return ExitCodes.Success;
        });
        return command;
    }
}
