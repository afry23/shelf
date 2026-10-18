using System.Text;

namespace Shelf.Core.Grouping;

/// <summary>An author name read as surname and given names.</summary>
/// <param name="SurnameKey">The comparable surname (last word of the surname part, folded), or null if the name has no letters.</param>
/// <param name="SortKey">"Surname, Given" as written, for sorting and folder names.</param>
public sealed record AuthorName(string? SurnameKey, string SortKey);

/// <summary>Author names as they appear in files: "Martin Kleppmann", "Kleppmann, Martin", "Jan van der Berg" (F-GRP-1).</summary>
public static class AuthorNames
{
    /// <summary>
    /// The author written into a sidecar when neither the file nor its name says who wrote the book
    /// (a sidecar needs one). It has no surname key, so unknown authors never match each other.
    /// </summary>
    public const string UnknownAuthor = "Unknown Author";

    private static readonly HashSet<string> Particles = new(StringComparer.Ordinal)
    {
        "van", "von", "der", "den", "de", "del", "della", "di", "da", "du", "la", "le", "ter", "ten", "zu", "zur",
        "bin", "al", "el", "dos", "das", "do", "ibn",
    };

    private static readonly HashSet<string> Suffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "jr", "jr.", "sr", "sr.", "ii", "iii", "iv",
    };

    public static AuthorName Parse(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var cleaned = string.Join(' ', name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (string.Equals(cleaned, UnknownAuthor, StringComparison.OrdinalIgnoreCase))
        {
            return new AuthorName(null, UnknownAuthor);
        }

        string surnamePart;
        string sortKey;
        var comma = cleaned.IndexOf(',', StringComparison.Ordinal);
        if (comma >= 0)
        {
            // "Surname, Given": already in sort order.
            surnamePart = cleaned[..comma].Trim();
            var given = cleaned[(comma + 1)..].Trim();
            sortKey = given.Length == 0 ? surnamePart : surnamePart + ", " + given;
        }
        else
        {
            var tokens = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
            var suffix = tokens.Count > 1 && Suffixes.Contains(tokens[^1]) ? tokens[^1] : null;
            if (suffix is not null)
            {
                tokens.RemoveAt(tokens.Count - 1);
            }

            if (tokens.Count == 0)
            {
                return new AuthorName(null, cleaned);
            }

            var surnameStart = tokens.Count - 1;
            while (surnameStart > 0 && IsParticle(tokens[surnameStart - 1]))
            {
                surnameStart--;
            }

            surnamePart = string.Join(' ', tokens.Skip(surnameStart));
            var given = string.Join(' ', tokens.Take(surnameStart));
            if (suffix is not null)
            {
                given = given.Length == 0 ? suffix : given + " " + suffix;
            }

            sortKey = given.Length == 0 ? surnamePart : surnamePart + ", " + given;
        }

        return new AuthorName(KeyOf(surnamePart), sortKey);
    }

    /// <summary>The distinct surname keys of a list of authors.</summary>
    public static IReadOnlyList<string> SurnameKeys(IEnumerable<string> authors)
    {
        ArgumentNullException.ThrowIfNull(authors);
        return [.. authors.Select(a => Parse(a).SurnameKey).OfType<string>().Distinct(StringComparer.Ordinal)];
    }

    /// <summary>True if the two author lists share a surname. Without authors on either side nothing matches.</summary>
    public static bool Match(IEnumerable<string> first, IEnumerable<string> second) =>
        SurnameKeys(first).Intersect(SurnameKeys(second), StringComparer.Ordinal).Any();

    private static bool IsParticle(string token) => char.IsLower(token[0]) && Particles.Contains(token);

    /// <summary>The last word of the surname part, folded, without punctuation ("O'Reilly" → "oreilly").</summary>
    private static string? KeyOf(string surnamePart)
    {
        var lastWord = surnamePart.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        if (lastWord is null)
        {
            return null;
        }

        var key = new StringBuilder();
        foreach (var c in TextFolding.Fold(lastWord))
        {
            if (char.IsLetterOrDigit(c))
            {
                key.Append(c);
            }
        }

        return key.Length == 0 ? null : key.ToString();
    }
}
