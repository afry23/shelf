using System.CommandLine;
using Shelf.Organize;

namespace Shelf.Cli.Commands;

/// <summary><c>shelf trash empty [--yes]</c> (F-DUP-4): the only way to delete files for good; a dry run unless <c>--yes</c>.</summary>
internal static class TrashCommand
{
    public static Command Create(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var command = new Command("trash", "Work with the trash of deleted files");
        command.Subcommands.Add(Empty(json, stdout, stderr, environment));
        return command;
    }

    private static Command Empty(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var yes = new Option<bool>("--yes") { Description = "Delete the files for good (without it, only show what would be deleted)" };
        var library = LibraryContext.CreateOption();
        var command = new Command("empty", "Delete everything in the trash for good (dry run unless --yes)");
        command.Options.Add(yes);
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            var doDelete = parseResult.GetValue(yes);
            var result = doDelete ? TrashEmptier.Empty(context.Root) : TrashEmptier.Measure(context.Root);
            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonOutput.Write(writer =>
                {
                    writer.WriteStartObject();
                    JsonOutput.Property(writer, "deleted", doDelete && result.Files > 0);
                    JsonOutput.Property(writer, "files", result.Files);
                    writer.WriteNumber("bytes", result.Bytes);
                    writer.WriteEndObject();
                }));
            }
            else if (result.Files == 0)
            {
                stdout.WriteLine("Trash is empty.");
            }
            else
            {
                var amount = $"{TextFormat.Plural(result.Files, "file", "files")} ({TextFormat.Size(result.Bytes)})";
                stdout.WriteLine(doDelete
                    ? $"Deleted {amount} for good."
                    : $"Would delete {amount} for good. Run with --yes to do it.");
            }

            return ExitCodes.Success;
        });
        return command;
    }
}
