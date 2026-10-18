using System.CommandLine;
using Shelf.Index;

namespace Shelf.Cli.Commands;

/// <summary><c>shelf merge &lt;work-a&gt; &lt;work-b&gt;</c> (F-GRP-4): the files of b go into a, then b is gone.</summary>
internal static class MergeCommand
{
    public static Command Create(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var first = WorkSupport.Argument("work-a");
        first.Description = "The work that stays: " + first.Description;
        var second = WorkSupport.Argument("work-b");
        second.Description = "The work that is merged into work-a and removed: " + second.Description;
        var library = LibraryContext.CreateOption();
        var command = new Command("merge", "Merge work-b into work-a: its files move into the folder of work-a");
        command.Arguments.Add(first);
        command.Arguments.Add(second);
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            var target = WorkSupport.Resolve(context, environment, parseResult.GetRequiredValue(first));
            var source = WorkSupport.Resolve(context, environment, parseResult.GetRequiredValue(second));
            WorkSupport.WriteMerge(stdout, stderr, parseResult.GetValue(json), LibraryEditor.Merge(context.Root, target, source));
            return ExitCodes.Success;
        });
        return command;
    }
}
