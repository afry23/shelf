using System.Text;
using System.Text.RegularExpressions;

namespace Shelf.Core.Grouping;

/// <summary>
/// The comparable form of a title (F-GRP-2): lower case, accents removed, edition notes removed, punctuation
/// turned into spaces, a leading article (der, die, das, the, a, an) removed. Subtitles are kept: removing them
/// would merge "Python: Introduction" with "Python: Advanced Topics".
/// </summary>
public static partial class TitleNormalizer
{
    private static readonly HashSet<string> Articles = new(StringComparer.Ordinal) { "der", "die", "das", "the", "a", "an" };

    // "(German Edition)", "(Second Edition)" and the like; the word in front is a language or an ordinal.
    [GeneratedRegex(@"\s*[\(\[]\s*[a-zäöüéèàçñ0-9\.\- ]{1,25}?\s+edition\s*[\)\]]", RegexOptions.CultureInvariant)]
    private static partial Regex ParenthesizedEdition();

    // "2nd Edition", "third ed.", "revised edition"; "Edition 4" and "Special Edition" stay.
    [GeneratedRegex(@"\b(?:\d+(?:st|nd|rd|th)|first|second|third|fourth|fifth|sixth|seventh|eighth|ninth|tenth|revised|updated|expanded)\s+(?:edition\b|ed\b\.?)", RegexOptions.CultureInvariant)]
    private static partial Regex OrdinalEdition();

    // "3. Auflage", "zweite Auflage".
    [GeneratedRegex(@"\b(?:\d+\.\s*|erste\s+|zweite\s+|dritte\s+|vierte\s+|fünfte\s+|überarbeitete\s+|neu\s+bearbeitete\s+)(?:auflage\b|aufl\b\.?)", RegexOptions.CultureInvariant)]
    private static partial Regex GermanEdition();

    public static string Normalize(string title)
    {
        ArgumentNullException.ThrowIfNull(title);
        var lower = title.ToLowerInvariant();
        lower = ParenthesizedEdition().Replace(lower, " ");
        lower = OrdinalEdition().Replace(lower, " ");
        lower = GermanEdition().Replace(lower, " ");

        // Letters and digits stay, everything else separates words.
        var folded = TextFolding.Fold(lower);
        var words = new List<string>();
        var word = new StringBuilder();
        foreach (var c in folded)
        {
            if (char.IsLetterOrDigit(c))
            {
                word.Append(c);
            }
            else if (word.Length > 0)
            {
                words.Add(word.ToString());
                word.Clear();
            }
        }

        if (word.Length > 0)
        {
            words.Add(word.ToString());
        }

        if (words.Count > 1 && Articles.Contains(words[0]))
        {
            words.RemoveAt(0);
        }

        return words.Count == 0 ? lower.Trim() : string.Join(' ', words);
    }
}
