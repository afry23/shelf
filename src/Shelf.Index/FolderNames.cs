using Shelf.Core.Organization;

namespace Shelf.Index;

/// <summary>Names of the folders and files shelf creates in a library.</summary>
internal static class FolderNames
{
    /// <summary>
    /// A name that is safe as one path component on every supported system: no separators or other reserved
    /// characters (a colon becomes " -", slashes and bars become "-", the rest is dropped), no leading or trailing
    /// dots and spaces, at most 120 UTF-8 bytes.
    /// </summary>
    public static string Sanitize(string name) => PathSanitizer.Segment(name);

    /// <summary>The path of the folder for a new work below the library root: Author / Title (Year).</summary>
    public static string WorkFolder(string root, string authorSortKey, string title, int? year) =>
        Path.Combine(root, Sanitize(authorSortKey), Sanitize(year is { } y ? $"{title} ({y})" : title));

    /// <summary>The folder itself if it is free; otherwise the first of "name (2)", "name (3)", ... that is free.</summary>
    public static string FreeFolder(string folder)
    {
        var candidate = folder;
        for (var n = 2; Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "book.json")); n++)
        {
            candidate = $"{folder} ({n})";
        }

        return candidate;
    }

    /// <summary>The name if no such file exists in the directory; otherwise "name (1).ext", "name (2).ext", ...</summary>
    public static string UniqueFileName(string directory, string fileName)
    {
        if (!File.Exists(Path.Combine(directory, fileName)))
        {
            return fileName;
        }

        var stem = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        for (var n = 1; ; n++)
        {
            var candidate = $"{stem} ({n}){extension}";
            if (!File.Exists(Path.Combine(directory, candidate)))
            {
                return candidate;
            }
        }
    }
}
