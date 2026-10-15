using System.Globalization;
using System.Text;

namespace Shelf.TestData.Content;

internal enum FileNamePattern
{
    AuthorTitleYear,
    Title,
    SnakeCase,
    Isbn13,
    TitleAuthor,
}

internal static class FileNames
{
    public const int MaxStemLength = 120;
    private const string Forbidden = "/\\:*?\"<>|";

    /// <summary>File-name stem (no extension) for a pattern. <see cref="FileNamePattern.Isbn13"/> needs an ISBN and otherwise falls back to the title.</summary>
    public static string Stem(FileNamePattern pattern, string author, string title, int year, string? isbn13) => Sanitize(pattern switch
    {
        FileNamePattern.AuthorTitleYear => $"{author} - {title} ({year.ToString(CultureInfo.InvariantCulture)})",
        FileNamePattern.SnakeCase => Snake(title, year),
        FileNamePattern.Isbn13 when isbn13 is not null => isbn13,
        FileNamePattern.TitleAuthor => $"{title} - {author}",
        _ => title,
    });

    /// <summary>
    /// Replaces forbidden characters by '-', collapses spaces, normalizes to NFC, trims trailing dots and spaces
    /// and cuts at <see cref="MaxStemLength"/> UTF-16 chars without splitting a surrogate pair.
    /// </summary>
    public static string Sanitize(string stem)
    {
        var builder = new StringBuilder(stem.Length);
        var previousSpace = false;
        foreach (var c in stem)
        {
            var replaced = Forbidden.Contains(c) || char.IsControl(c) ? '-' : c;
            var space = replaced == ' ';
            if (space && previousSpace)
            {
                continue;
            }

            previousSpace = space;
            builder.Append(replaced);
        }

        var result = builder.ToString().Normalize(NormalizationForm.FormC).Trim(' ', '.');
        if (result.Length > MaxStemLength)
        {
            var length = MaxStemLength;
            if (char.IsHighSurrogate(result[length - 1]))
            {
                length--;
            }

            result = result[..length].TrimEnd(' ', '.');
        }

        return result.Length == 0 ? "untitled" : result;
    }

    /// <summary>Returns <paramref name="stem"/> + extension, or a " (1)", " (2)", ... variant that is not in <paramref name="used"/>; records the result.</summary>
    public static string MakeUnique(string stem, string extension, HashSet<string> used)
    {
        var candidate = stem + extension;
        for (var i = 1; !used.Add(candidate); i++)
        {
            candidate = stem + " (" + i.ToString(CultureInfo.InvariantCulture) + ")" + extension;
        }

        return candidate;
    }

    private static string Snake(string title, int year)
    {
        var builder = new StringBuilder();
        var pendingSeparator = false;
        foreach (var c in title.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
            {
                if (pendingSeparator && builder.Length > 0)
                {
                    builder.Append('_');
                }

                pendingSeparator = false;
                builder.Append(c);
            }
            else
            {
                pendingSeparator = true;
            }
        }

        builder.Append('_').Append(year.ToString(CultureInfo.InvariantCulture));
        return builder.ToString();
    }
}
