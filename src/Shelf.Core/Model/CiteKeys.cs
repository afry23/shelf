using System.Text;
using Shelf.Core.Grouping;

namespace Shelf.Core.Model;

/// <summary>
/// The key under which a work is cited. It must survive as a BibTeX key, so it has no spaces, commas, braces or
/// other characters that BibTeX and its relatives treat as syntax.
/// </summary>
public static class CiteKeys
{
    public const string Rule = "may only contain letters, digits and - _ : . + /";

    // Words that do not name a book; the first word of a title that is not one of these is used.
    private static readonly HashSet<string> SkippedWords = new(StringComparer.Ordinal)
    {
        "a", "an", "the", "of", "on", "in", "to", "for", "and", "with",
        "der", "die", "das", "ein", "eine", "und", "von", "zu", "im", "am",
    };

    public static bool IsValid(string key) =>
        key.Length > 0 && key.All(c => char.IsLetterOrDigit(c) || c is '-' or '_' or ':' or '.' or '+' or '/');

    /// <summary>
    /// The key that author, year and title suggest: surname of the first author, year and the first significant word of
    /// the title, folded to lower case ("ibryam2019kubernetes"). Null if the first author is not known.
    /// </summary>
    public static string? Suggest(IReadOnlyList<string> authors, int? year, string title)
    {
        ArgumentNullException.ThrowIfNull(authors);
        ArgumentNullException.ThrowIfNull(title);
        var surname = authors.Count == 0 ? null : AuthorNames.Parse(authors[0]).SurnameKey;
        if (surname is null)
        {
            return null;
        }

        var key = new StringBuilder(surname);
        if (year is { } y)
        {
            key.Append(y.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        var words = TitleNormalizer.Normalize(title).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var word = words.FirstOrDefault(w => !SkippedWords.Contains(w)) ?? words.FirstOrDefault();
        key.Append(word);
        return key.ToString();
    }

    /// <summary>
    /// <paramref name="key"/> if <paramref name="isTaken"/> does not know it, else the key with a letter (a to z) and then
    /// with a number (-2, -3, ...) appended.
    /// </summary>
    public static string MakeUnique(string key, Func<string, bool> isTaken)
    {
        ArgumentNullException.ThrowIfNull(isTaken);
        if (!isTaken(key))
        {
            return key;
        }

        for (var letter = 'a'; letter <= 'z'; letter++)
        {
            if (!isTaken(key + letter))
            {
                return key + letter;
            }
        }

        for (var number = 2; ; number++)
        {
            var candidate = key + "-" + number.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!isTaken(candidate))
            {
                return candidate;
            }
        }
    }
}
