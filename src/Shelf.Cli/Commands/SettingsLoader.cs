using Shelf.Core.Configuration;

namespace Shelf.Cli.Commands;

/// <summary>Loads the settings a command works with and reports unknown keys on standard error.</summary>
internal static class SettingsLoader
{
    /// <summary>
    /// Settings from the global config and, when a library root is given, from its config.
    /// Missing files are empty configurations. Unknown keys become warnings on <paramref name="stderr"/>.
    /// </summary>
    /// <exception cref="ConfigException">A file is unreadable or has invalid content.</exception>
    public static ShelfSettings Load(CliEnvironment environment, TextWriter stderr, string? libraryRoot = null)
    {
        var global = ConfigFile.Load(ConfigPaths.Global(environment.GetVariable, environment.HomeDirectory));
        var library = libraryRoot is null ? null : ConfigFile.Load(ConfigPaths.Library(libraryRoot));
        var settings = ShelfSettings.Resolve(global, library);
        foreach (var warning in settings.Warnings)
        {
            stderr.WriteLine($"warning: {warning}");
        }

        return settings;
    }
}
