using System.Text.RegularExpressions;

namespace Shelf.Extraction;

/// <summary>
/// Guesses author, title and year from a file name like <c>Author - Title (Year).pdf</c> (F-IMP-3).
/// Only a fallback: the result is weak evidence and is marked as such.
/// </summary>
public static partial class FileNameMetadata
{
    private static readonly string[] Dashes = [" - ", " – ", " — "];

    // A year in round or square brackets at the end: 1000 to 2099.
    [GeneratedRegex(@"\s*[\(\[]((?:1[0-9]|20)[0-9]{2})[\)\]]\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex TrailingYear();

    [GeneratedRegex(@"\s+(?:&|and|und)\s+|\s*;\s*", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex AuthorSeparator();

    public sealed record Guess(string? Title, IReadOnlyList<string> Authors, int? Year);

    public static Guess Parse(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        var name = Path.GetFileNameWithoutExtension(fileName).Trim();
        if (!name.Contains(' ', StringComparison.Ordinal))
        {
            name = name.Replace('_', ' ');
        }

        int? year = null;
        var yearMatch = TrailingYear().Match(name);
        if (yearMatch.Success)
        {
            year = int.Parse(yearMatch.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            name = name[..yearMatch.Index].TrimEnd();
        }

        string? authorPart = null;
        var title = name;
        foreach (var dash in Dashes)
        {
            var index = name.IndexOf(dash, StringComparison.Ordinal);
            if (index > 0)
            {
                authorPart = name[..index].Trim();
                title = name[(index + dash.Length)..].Trim();
                break;
            }
        }

        // Single spaces everywhere ("Quiet   Systems" from sloppy names).
        title = Regex.Replace(title, @"\s+", " ");
        IReadOnlyList<string> authors = authorPart is null
            ? []
            : [.. AuthorSeparator().Split(authorPart).Select(a => Regex.Replace(a, @"\s+", " ").Trim()).Where(a => a.Length > 0)];

        // "Jane Doe - " has no title: treat the whole name as the title instead of inventing an author.
        if (title.Length == 0)
        {
            return new Guess(authorPart, [], year);
        }

        return new Guess(title, authors, year);
    }
}
