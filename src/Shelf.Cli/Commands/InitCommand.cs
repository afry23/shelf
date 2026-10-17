using System.CommandLine;
using System.Text.Json;
using Shelf.Core.Libraries;

namespace Shelf.Cli.Commands;

/// <summary><c>shelf init &lt;root&gt;</c>: turns a folder into a library (F-LIB-1).</summary>
internal static class InitCommand
{
    public static Command Create(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var root = new Argument<string>("root")
        {
            Description = "Folder that becomes the library; created if it does not exist",
        };
        // An unknown option must not be taken as the folder name (write ./-x for a folder that starts with a dash).
        root.Validators.Add(result =>
        {
            var value = result.GetValueOrDefault<string>();
            if (value is not null && value.StartsWith('-'))
            {
                result.AddError(ParseErrorMessages.Describe([value])!);
            }
        });
        var command = new Command("init", "Create a library in a folder");
        command.Arguments.Add(root);
        command.SetAction(parseResult =>
        {
            // Settings first: an invalid config must stop the command before anything is created.
            SettingsLoader.Load(environment, stderr);

            var path = Path.GetFullPath(parseResult.GetRequiredValue(root), environment.WorkingDirectory);
            LibraryInitializer.Initialize(path);
            path = Path.TrimEndingDirectorySeparator(path);

            if (parseResult.GetValue(json))
            {
                stdout.WriteLine(JsonOutput.Object(("root", path)));
            }
            else
            {
                stdout.WriteLine($"Initialized library in {path}");
            }

            return ExitCodes.Success;
        });
        return command;
    }
}
