using System.CommandLine;
using Shelf.Index;

namespace Shelf.Cli.Commands;

/// <summary><c>shelf undo</c> (F-ORG-6): reverts the last organize or tag run; a file that changed since is kept and reported.</summary>
internal static class UndoCommand
{
    public static Command Create(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var library = LibraryContext.CreateOption();
        var command = new Command("undo", "Revert the last organize, tag or dupes run");
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            var result = LibraryOrganizer.Undo(context.Root);
            if (result is null)
            {
                stderr.WriteLine("error: nothing to undo");
                return ExitCodes.Error;
            }

            foreach (var skip in result.Skipped)
            {
                stderr.WriteLine($"warning: kept {skip.Path}: {skip.Reason}");
            }

            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonOutput.Write(writer =>
                {
                    writer.WriteStartObject();
                    JsonOutput.Property(writer, "journal", result.Journal);
                    JsonOutput.Property(writer, "label", result.Label);
                    JsonOutput.Property(writer, "reverted", result.Reverted);
                    JsonOutput.Property(writer, "skipped", result.Skipped.Count);
                    writer.WriteEndObject();
                }));
            }
            else
            {
                stdout.WriteLine($"Reverted the last {result.Label} run ({result.Journal})");
            }

            return result.Skipped.Count == 0 ? ExitCodes.Success : ExitCodes.Error;
        });
        return command;
    }
}
