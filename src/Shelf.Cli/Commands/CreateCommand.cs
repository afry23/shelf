using System.CommandLine;
using Shelf.Index;

namespace Shelf.Cli.Commands;

/// <summary><c>shelf create &lt;file&gt;</c> (F-GRP-9): a work from one file, without grouping it into a work it matches.</summary>
internal static class CreateCommand
{
    public static Command Create(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var source = new Argument<string>("file") { Description = "The file of the new work" };
        var values = new WorkValuesOptions();
        var move = new Option<bool>("--move") { Description = "Move the file into the library instead of copying it" };
        var kind = new Option<string?>("--kind") { Description = "Kind of the new work: book, paper or article (default: the file decides)" };
        var library = LibraryContext.CreateOption();
        var command = new Command("create", "Create a work from a file by hand, without grouping");
        command.Arguments.Add(source);
        values.AddTo(command);
        command.Options.Add(move);
        command.Options.Add(kind);
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            var result = LibraryFiles.CreateWork(context.Root, parseResult.GetRequiredValue(source), values.Read(parseResult), parseResult.GetValue(move), environment.WorkingDirectory, WorkKinds.Parse(parseResult.GetValue(kind)));

            CoverReport.Warn(LibraryCovers.FillWorks(context.Root, [result.Work]), stderr);
            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonOutput.Write(writer =>
                {
                    writer.WriteStartObject();
                    JsonOutput.Property(writer, "id", result.Work.ToString());
                    JsonOutput.Property(writer, "file", result.File);
                    JsonOutput.Property(writer, "folder", result.Folder);
                    writer.WriteEndObject();
                }));
            }
            else
            {
                stdout.WriteLine($"Created {result.Work} from {result.File}");
            }

            return ExitCodes.Success;
        });
        return command;
    }
}
