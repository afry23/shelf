using System.CommandLine;
using Shelf.Index;

namespace Shelf.Cli.Commands;

/// <summary>
/// <c>shelf covers</c> (F-IMP-5): makes the cover of every work that lacks one. Import, scan and reindex do that for the files they
/// read; this is for a library from before covers existed, and for works whose files gave no cover then.
/// </summary>
internal static class CoversCommand
{
    public static Command Create(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var library = LibraryContext.CreateOption();
        var command = new Command("covers", "Make the cover image of every work that has none");
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            var result = LibraryCovers.FillAll(context.Root);
            CoverReport.Warn(result, stderr);

            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonOutput.Object(
                    ("created", result.Created),
                    ("existing", result.Existing),
                    ("without", result.Without),
                    ("problems", result.Problems.Count)));
            }
            else
            {
                var none = result.Without switch
                {
                    0 => "every work has one",
                    1 => "1 work has none",
                    var n => $"{n} works have none",
                };
                stdout.WriteLine($"Created {result.Created} {(result.Created == 1 ? "cover" : "covers")}; {none}.");
            }

            return ExitCodes.Success;
        });
        return command;
    }
}
