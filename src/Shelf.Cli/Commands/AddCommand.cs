using System.CommandLine;
using Shelf.Index;

namespace Shelf.Cli.Commands;

/// <summary><c>shelf add &lt;work&gt; &lt;file&gt;</c> (F-GRP-7): a file, such as a new version of a book, joins a work.</summary>
internal static class AddCommand
{
    public static Command Create(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var reference = WorkSupport.Argument();
        var source = new Argument<string>("file") { Description = "The file to add" };
        var replace = new Option<string?>("--replace") { Description = "File of the work that the new file replaces; it goes to the trash" };
        var carryNotes = new Option<bool>("--carry-notes") { Description = "With --replace: reading position and notes go to the new file" };
        var move = new Option<bool>("--move") { Description = "Move the file into the library instead of copying it" };
        var library = LibraryContext.CreateOption();
        var command = new Command("add", "Add a file to a work, for example a new version of a book");
        command.Arguments.Add(reference);
        command.Arguments.Add(source);
        command.Options.Add(replace);
        command.Options.Add(carryNotes);
        command.Options.Add(move);
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            if (parseResult.GetValue(carryNotes) && parseResult.GetValue(replace) is null)
            {
                stderr.WriteLine("error: --carry-notes needs --replace");
                return ExitCodes.Usage;
            }

            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            var work = WorkSupport.Resolve(context, environment, parseResult.GetRequiredValue(reference));
            var options = new AddOptions(parseResult.GetValue(move), parseResult.GetValue(replace), parseResult.GetValue(carryNotes));
            var result = LibraryFiles.AddFile(context.Root, work, parseResult.GetRequiredValue(source), options, environment.WorkingDirectory);

            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonOutput.Write(writer =>
                {
                    writer.WriteStartObject();
                    JsonOutput.Property(writer, "id", result.Work.ToString());
                    JsonOutput.Property(writer, "file", result.File);
                    if (result.Replaced is { } replaced)
                    {
                        JsonOutput.Property(writer, "replaced", replaced);
                    }

                    writer.WriteEndObject();
                }));
            }
            else
            {
                stdout.WriteLine(result.Replaced is { } old
                    ? $"Replaced {old} by {result.File} in {result.Work}. Undo with shelf undo."
                    : $"Added {result.File} to {result.Work}");
            }

            return ExitCodes.Success;
        });
        return command;
    }
}
