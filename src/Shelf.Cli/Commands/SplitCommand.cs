using System.CommandLine;
using Shelf.Index;

namespace Shelf.Cli.Commands;

/// <summary><c>shelf split &lt;work&gt; &lt;file&gt;</c> (F-GRP-4): a file becomes a work of its own.</summary>
internal static class SplitCommand
{
    public static Command Create(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var reference = WorkSupport.Argument();
        var file = new Argument<string>("file") { Description = "File name of the file in the work, or its path" };
        var library = LibraryContext.CreateOption();
        var command = new Command("split", "Split a file off a work into a work of its own");
        command.Arguments.Add(reference);
        command.Arguments.Add(file);
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            var work = WorkSupport.Resolve(context, environment, parseResult.GetRequiredValue(reference));
            var result = LibraryEditor.Split(context.Root, work, parseResult.GetRequiredValue(file), environment.WorkingDirectory);

            // The new work has no cover yet: its file is read for one.
            CoverReport.Warn(LibraryCovers.FillWorks(context.Root, [result.NewWork]), stderr);
            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonOutput.Write(writer =>
                {
                    writer.WriteStartObject();
                    JsonOutput.Property(writer, "id", result.Source.ToString());
                    JsonOutput.Property(writer, "file", result.File);
                    JsonOutput.Property(writer, "work", result.NewWork.ToString());
                    JsonOutput.Property(writer, "folder", result.Folder);
                    writer.WriteEndObject();
                }));
            }
            else
            {
                stdout.WriteLine($"Split {result.File} off {result.Source} into {result.NewWork}");
            }

            return ExitCodes.Success;
        });
        return command;
    }
}
