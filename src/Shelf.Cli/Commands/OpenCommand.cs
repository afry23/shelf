using System.CommandLine;
using Shelf.Index;

namespace Shelf.Cli.Commands;

/// <summary><c>shelf open &lt;work&gt; [--format pdf]</c>: starts the application the system uses for the file.</summary>
internal static class OpenCommand
{
    public static Command Create(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var reference = WorkSupport.Argument();
        var format = new Option<string?>("--format") { Description = "File format to open, such as pdf or epub (default: the first file of the work)" };
        var library = LibraryContext.CreateOption();
        var command = new Command("open", "Open a work with the default application");
        command.Arguments.Add(reference);
        command.Options.Add(format);
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            var work = WorkSupport.Resolve(context, environment, parseResult.GetRequiredValue(reference));
            var sidecar = LibraryEditor.Read(context.Root, work);
            var wanted = parseResult.GetValue(format)?.Trim().TrimStart('.').ToLowerInvariant();
            var file = wanted is null or ""
                ? sidecar.Files[0]
                : sidecar.Files.FirstOrDefault(f => ShowCommand.IndexWriterFormat(f.Name) == wanted)
                    ?? throw new LibraryEditException(
                        $"{work.Id} has no {wanted} file (available: {string.Join(", ", sidecar.Files.Select(f => ShowCommand.IndexWriterFormat(f.Name)).Distinct())})");

            var relative = IndexRelative(work.Directory, file.Name);
            var path = LibraryEditor.FilePath(context.Root, work, file.Name);
            if (!File.Exists(path))
            {
                throw new LibraryEditException($"{relative} does not exist");
            }

            try
            {
                environment.FileOpener.Open(path);
            }
            catch (IOException e)
            {
                throw new LibraryEditException($"cannot open {relative}: {e.Message}");
            }

            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonOutput.Object(("id", work.Id.ToString()), ("file", path)));
            }
            else
            {
                stdout.WriteLine($"Opening {path}");
            }

            return ExitCodes.Success;
        });
        return command;
    }

    private static string IndexRelative(string directory, string name) => directory == "." ? name : directory + "/" + name;
}
