using System.Text;

namespace Shelf.Core.Libraries;

/// <summary>A folder cannot become a library (already one, inside one, or not a folder).</summary>
public sealed class LibraryInitException(string message, Exception? innerException = null) : Exception(message, innerException);

/// <summary>Creates the <c>.shelf</c> folder of a new library (PRD 10.1, F-LIB-1).</summary>
public static class LibraryInitializer
{
    public const string ReviewFileName = "review.json";
    public const string ConfigFileName = "config.toml";

    private const string MarkerFolder = ".shelf";

    /// <summary>The queue of an empty library: no entries yet.</summary>
    public const string EmptyReviewQueue = "{\n  \"schema\": 1,\n  \"entries\": []\n}\n";

    /// <summary>
    /// Makes <paramref name="root"/> (an absolute path) a library: creates the folder if needed, then
    /// <c>.shelf/config.toml</c> (a commented template) and <c>.shelf/review.json</c>. Existing books are not touched.
    /// If anything fails, the <c>.shelf</c> folder this call created is removed again.
    /// </summary>
    /// <param name="root">Absolute path of the new library.</param>
    /// <param name="createIndex">Creates the index database at the given path; part of the same all-or-nothing step.</param>
    /// <exception cref="LibraryInitException"/>
    public static void Initialize(string root, Action<string>? createIndex = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(root);
        if (!Path.IsPathRooted(root))
        {
            throw new ArgumentException("The library root must be an absolute path.", nameof(root));
        }

        root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        if (File.Exists(root))
        {
            throw new LibraryInitException($"'{root}' is a file, not a folder");
        }

        for (var parent = Path.GetDirectoryName(root); parent is not null; parent = Path.GetDirectoryName(parent))
        {
            if (Directory.Exists(Path.Combine(parent, MarkerFolder)))
            {
                throw new LibraryInitException($"'{root}' is inside the library '{parent}'");
            }
        }

        var marker = Path.Combine(root, MarkerFolder);
        if (Directory.Exists(marker))
        {
            throw new LibraryInitException($"'{root}' is already a shelf library");
        }

        try
        {
            Directory.CreateDirectory(marker);
            try
            {
                var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
                File.WriteAllText(Path.Combine(marker, ConfigFileName), Configuration.ConfigTemplate.Library, utf8);
                File.WriteAllText(Path.Combine(marker, ReviewFileName), EmptyReviewQueue, utf8);
                createIndex?.Invoke(Path.Combine(marker, "index.db"));
            }
            catch
            {
                Directory.Delete(marker, recursive: true);
                throw;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new LibraryInitException($"cannot create the library in '{root}': {e.Message}", e);
        }
    }
}
