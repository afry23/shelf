using System.CommandLine;
using Shelf.Index;

namespace Shelf.Cli.Commands;

/// <summary>
/// <c>shelf delete &lt;work&gt; [file] [--yes]</c> (F-DUP-7): a file or a whole work goes to the trash, as for duplicates (F-DUP-4).
/// A dry run unless <c>--yes</c>; <c>shelf undo</c> brings it back.
/// </summary>
internal static class DeleteCommand
{
    public static Command Create(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var reference = WorkSupport.Argument();
        var file = new Argument<string?>("file")
        {
            Description = "File name of the file in the work, or its path; without it the whole work is deleted",
            Arity = ArgumentArity.ZeroOrOne,
        };
        var yes = new Option<bool>("--yes") { Description = "Move it to the trash (without it, only show what would be moved)" };
        var library = LibraryContext.CreateOption();
        var command = new Command("delete", "Move a file or a whole work to the trash (dry run unless --yes)");
        command.Arguments.Add(reference);
        command.Arguments.Add(file);
        command.Options.Add(yes);
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            var work = WorkSupport.Resolve(context, environment, parseResult.GetRequiredValue(reference));
            var name = parseResult.GetValue(file);
            var doDelete = parseResult.GetValue(yes);
            DeleteResult result;
            if (name is null)
            {
                result = doDelete ? LibraryDeleter.DeleteWork(context.Root, work) : LibraryDeleter.PlanWork(context.Root, work);
            }
            else
            {
                result = doDelete
                    ? LibraryDeleter.DeleteFile(context.Root, work, name, environment.WorkingDirectory)
                    : LibraryDeleter.PlanFile(context.Root, work, name, environment.WorkingDirectory);
            }

            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonOutput.Write(writer =>
                {
                    writer.WriteStartObject();
                    JsonOutput.Property(writer, "work", result.Work.ToString());
                    JsonOutput.Property(writer, "deleted", doDelete);
                    JsonOutput.Property(writer, "trashed", result.Trashed);
                    JsonOutput.Property(writer, "workRemoved", result.WorkRemoved);
                    WorkSupport.WriteStrings(writer, "files", result.Files);
                    writer.WriteEndObject();
                }));
            }
            else
            {
                var what = name is null
                    ? $"{result.Work} to the trash ({TextFormat.Plural(result.Trashed, "file", "files")})"
                    : $"{result.Files[0]} to the trash";
                var lastFile = name is not null && result.WorkRemoved;
                stdout.WriteLine(doDelete
                    ? $"Moved {what}{(lastFile ? $"; it was the last file, so removed the work {result.Work}" : string.Empty)}. Undo with shelf undo."
                    : $"Would move {what}{(lastFile ? $"; it is the last file, so the work {result.Work} would go too" : string.Empty)}. Run with --yes to do it.");
            }

            return ExitCodes.Success;
        });
        return command;
    }
}
