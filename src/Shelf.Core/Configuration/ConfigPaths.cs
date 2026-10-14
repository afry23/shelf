namespace Shelf.Core.Configuration;

/// <summary>Locations of the configuration files (PRD 9.2). Pure functions: nothing here touches the file system or environment.</summary>
public static class ConfigPaths
{
    private const string FileName = "config.toml";

    /// <summary>
    /// The global config file: <c>$XDG_CONFIG_HOME/shelf/config.toml</c> if that variable is set to an
    /// absolute path, otherwise <c>&lt;home&gt;/.config/shelf/config.toml</c> (also on macOS).
    /// </summary>
    public static string Global(Func<string, string?> getVariable, string homeDirectory)
    {
        ArgumentNullException.ThrowIfNull(getVariable);
        ArgumentException.ThrowIfNullOrEmpty(homeDirectory);

        var xdg = getVariable("XDG_CONFIG_HOME");
        var baseDirectory = !string.IsNullOrEmpty(xdg) && Path.IsPathRooted(xdg)
            ? xdg
            : Path.Combine(homeDirectory, ".config");
        return Path.Combine(Path.TrimEndingDirectorySeparator(baseDirectory), "shelf", FileName);
    }

    /// <summary>The per-library config file: <c>&lt;root&gt;/.shelf/config.toml</c>.</summary>
    public static string Library(string libraryRoot)
    {
        ArgumentException.ThrowIfNullOrEmpty(libraryRoot);
        return Path.Combine(Path.TrimEndingDirectorySeparator(libraryRoot), ".shelf", FileName);
    }
}
