using System.CommandLine;
using Shelf.Core.Configuration;
using Shelf.Core.Libraries;

namespace Shelf.Cli.Commands;

/// <summary>The library a command works on and its settings.</summary>
internal sealed record LibraryContext(string Root, ShelfSettings Settings)
{
    /// <summary>The <c>--library</c> option of commands that work on an existing library.</summary>
    public static Option<string?> CreateOption() => new("--library")
    {
        Description = "Library to use: a name from the global config or a folder (default: the current folder or a parent, else default_library)",
    };

    /// <summary>
    /// Finds the library (option, working directory, default library) and loads global and library config.
    /// Unknown keys are reported once on <paramref name="stderr"/>.
    /// </summary>
    /// <exception cref="ConfigException"/>
    /// <exception cref="LibraryNotFoundException"/>
    public static LibraryContext Open(string? libraryOption, CliEnvironment environment, TextWriter stderr)
    {
        var global = ConfigFile.Load(ConfigPaths.Global(environment.GetVariable, environment.HomeDirectory));
        var root = LibraryLocator.Resolve(
            libraryOption,
            environment.WorkingDirectory,
            ShelfSettings.Resolve(global, library: null),
            Directory.Exists).Root;

        var settings = ShelfSettings.Resolve(global, ConfigFile.Load(ConfigPaths.Library(root)));
        foreach (var warning in settings.Warnings)
        {
            stderr.WriteLine($"warning: {warning}");
        }

        return new LibraryContext(root, settings);
    }
}
