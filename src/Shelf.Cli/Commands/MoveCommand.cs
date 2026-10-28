using System.CommandLine;
using Shelf.Index;

namespace Shelf.Cli.Commands;

/// <summary>
/// <c>shelf move &lt;work&gt; &lt;file&gt; (--to &lt;work&gt; | --new) [--yes]</c> (F-GRP-8): a file goes to another work or to a new one.
/// A dry run unless <c>--yes</c>; <c>shelf undo</c> reverts it.
/// </summary>
internal static class MoveCommand
{
    public static Command Create(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var reference = WorkSupport.Argument();
        var file = new Argument<string>("file") { Description = "File name of the file in the work, or its path" };
        var to = new Option<string?>("--to") { Description = "The work that receives the file (ID, folder or file path)" };
        var makeNew = new Option<bool>("--new") { Description = "Make a new work of the file" };
        var values = new WorkValuesOptions();
        var yes = new Option<bool>("--yes") { Description = "Move the file (without it, only show what would be moved)" };
        var library = LibraryContext.CreateOption();
        var command = new Command("move", "Move a file to another work or to a new one (dry run unless --yes)");
        command.Arguments.Add(reference);
        command.Arguments.Add(file);
        command.Options.Add(to);
        command.Options.Add(makeNew);
        values.AddTo(command);
        command.Options.Add(yes);
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            var targetReference = parseResult.GetValue(to);
            var isNew = parseResult.GetValue(makeNew);
            if (targetReference is null == !isNew || (targetReference is not null && isNew))
            {
                stderr.WriteLine("error: give either --to <work> or --new");
                return ExitCodes.Usage;
            }

            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            var work = WorkSupport.Resolve(context, environment, parseResult.GetRequiredValue(reference));
            var target = targetReference is null ? null : WorkSupport.Resolve(context, environment, targetReference);
            var name = parseResult.GetRequiredValue(file);
            var doMove = parseResult.GetValue(yes);
            var result = doMove
                ? LibraryFiles.MoveFile(context.Root, work, name, target, values.Read(parseResult), environment.WorkingDirectory)
                : LibraryFiles.PlanMove(context.Root, work, name, target, environment.WorkingDirectory);
            if (doMove && target is null)
            {
                CoverReport.Warn(LibraryCovers.FillWorks(context.Root, [result.Target]), stderr);
            }

            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonOutput.Write(writer =>
                {
                    writer.WriteStartObject();
                    JsonOutput.Property(writer, "id", result.Source.ToString());
                    JsonOutput.Property(writer, "file", result.File);
                    JsonOutput.Property(writer, "moved", doMove);
                    if (doMove || target is not null)
                    {
                        JsonOutput.Property(writer, "work", result.Target.ToString());
                    }

                    JsonOutput.Property(writer, "workRemoved", result.SourceRemoved);
                    writer.WriteEndObject();
                }));
            }
            else
            {
                var destination = doMove || target is not null ? result.Target.ToString() : "a new work";
                var lastFile = result.SourceRemoved ? $"; it is the last file, so the work {result.Source} goes too" : string.Empty;
                stdout.WriteLine(doMove
                    ? $"Moved {result.File} to {destination}{(result.SourceRemoved ? $"; it was the last file, so removed the work {result.Source}" : string.Empty)}. Undo with shelf undo."
                    : $"Would move {result.File} from {result.Source} to {destination}{lastFile}. Run with --yes to do it.");
            }

            return ExitCodes.Success;
        });
        return command;
    }
}
