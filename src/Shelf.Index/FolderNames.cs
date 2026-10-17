using System.Text;

namespace Shelf.Index;

/// <summary>Names of the folders and files shelf creates in a library.</summary>
internal static class FolderNames
{
    private const int MaxBytes = 120;

    /// <summary>
    /// A name that is safe as one path component on every supported system: no separators or other reserved
    /// characters (a colon becomes " -", slashes and bars become "-", the rest is dropped), no leading or trailing
    /// dots and spaces, at most 120 UTF-8 bytes.
    /// </summary>
    public static string Sanitize(string name)
    {
        var builder = new StringBuilder(name.Length);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            switch (c)
            {
                case ':':
                    // "Flatland: A Romance" reads as "Flatland - A Romance"; a colon inside a word becomes a hyphen.
                    builder.Append(i + 1 < name.Length && name[i + 1] == ' ' ? " -" : "-");
                    break;
                case '/' or '\\' or '|':
                    builder.Append('-');
                    break;
                case '*' or '?' or '"' or '<' or '>':
                    break;
                default:
                    if (!char.IsControl(c))
                    {
                        builder.Append(c);
                    }

                    break;
            }
        }

        var cleaned = string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries)).Trim('.', ' ');
        while (Encoding.UTF8.GetByteCount(cleaned) > MaxBytes)
        {
            cleaned = cleaned[..^1];
        }

        cleaned = cleaned.TrimEnd('.', ' ');
        return cleaned.Length == 0 ? "_" : cleaned;
    }

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
