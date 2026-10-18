using Shelf.Core.Configuration;

namespace Shelf.Core.Libraries;

/// <summary>Where the library of a command came from.</summary>
public enum LibrarySource
{
    /// <summary>The <c>--library</c> option (a configured name or a path).</summary>
    Option,

    /// <summary>The working directory or one of its parents.</summary>
    WorkingDirectory,

    /// <summary><c>default_library</c> of the global config.</summary>
    DefaultLibrary,
}

public sealed record LibraryResolution(string Root, LibrarySource Source);

/// <summary>No library could be determined, or the chosen folder is not a library.</summary>
public sealed class LibraryNotFoundException(string message) : Exception(message);

/// <summary>
/// Chooses the library a command works on: the option, else the working directory (or a parent) that
/// contains a <c>.shelf</c> folder, else the default library (F-LIB-1). Does not touch the file system itself.
/// </summary>
public static class LibraryLocator
{
    private const string MarkerFolder = ".shelf";

    /// <param name="option">Value of <c>--library</c>: a name from the global config or a path; null if not given.</param>
    /// <param name="directoryExists">Answers whether an absolute directory exists.</param>
    /// <exception cref="LibraryNotFoundException"/>
    public static LibraryResolution Resolve(
        string? option,
        string workingDirectory,
        ShelfSettings settings,
        Func<string, bool> directoryExists)
    {
        ArgumentException.ThrowIfNullOrEmpty(workingDirectory);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(directoryExists);

        if (!string.IsNullOrEmpty(option))
        {
            var named = settings.Libraries.FirstOrDefault(l => l.Name == option);
            var path = named?.Path ?? Path.GetFullPath(option, workingDirectory);
            return IsLibrary(path, directoryExists)
                ? new LibraryResolution(Path.TrimEndingDirectorySeparator(path), LibrarySource.Option)
                : throw new LibraryNotFoundException($"'{option}' is not a shelf library (no {MarkerFolder} folder in '{path}')");
        }

        for (var directory = Path.GetFullPath(workingDirectory); directory is not null; directory = Path.GetDirectoryName(directory))
        {
            if (IsLibrary(directory, directoryExists))
            {
                return new LibraryResolution(Path.TrimEndingDirectorySeparator(directory), LibrarySource.WorkingDirectory);
            }
        }

        if (settings.DefaultLibrary is { } defaultName)
        {
            // Resolve() guarantees that the default names a configured library.
            var entry = settings.Libraries.First(l => l.Name == defaultName);
            return IsLibrary(entry.Path, directoryExists)
                ? new LibraryResolution(entry.Path, LibrarySource.DefaultLibrary)
                : throw new LibraryNotFoundException(
                    $"default library '{defaultName}' is not a shelf library (no {MarkerFolder} folder in '{entry.Path}')");
        }

        throw new LibraryNotFoundException(
            "no library found: run shelf inside a library, pass --library, or set default_library in the global config");
    }

    private static bool IsLibrary(string directory, Func<string, bool> directoryExists) =>
        directoryExists(Path.Combine(directory, MarkerFolder));
}
