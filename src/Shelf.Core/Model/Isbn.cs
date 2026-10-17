using System.Text;
using System.Text.RegularExpressions;

namespace Shelf.Core.Model;

/// <summary>
/// ISBN detection and validation (F-IMP-4). Results are always ISBN-13 digits without separators.
/// </summary>
public static partial class Isbn
{
    // 13 digits (978/979 prefix optional in the pattern so that ISBN-10 is matched too) with optional
    // hyphens or spaces; "ISBN" in front is captured because an ISBN-10 is only trusted with that word.
    [GeneratedRegex(@"(?<![0-9])(?<prefix>ISBN(?:-1[03])?:?\s*)?(?<num>(?:97[89][-\s]?)?(?:[0-9][-\s]?){9}[0-9Xx])(?![0-9])", RegexOptions.CultureInvariant)]
    private static partial Regex Candidate();

    public static bool IsValid13(string digits)
    {
        if (digits.Length != 13 || !digits.All(char.IsAsciiDigit))
        {
            return false;
        }

        var sum = 0;
        for (var i = 0; i < 13; i++)
        {
            sum += (digits[i] - '0') * (i % 2 == 0 ? 1 : 3);
        }

        return sum % 10 == 0;
    }

    public static bool IsValid10(string digits)
    {
        if (digits.Length != 10 || !digits[..9].All(char.IsAsciiDigit) || !(char.IsAsciiDigit(digits[9]) || digits[9] is 'X' or 'x'))
        {
            return false;
        }

        var sum = 0;
        for (var i = 0; i < 10; i++)
        {
            var value = digits[i] is 'X' or 'x' ? 10 : digits[i] - '0';
            sum += (10 - i) * value;
        }

        return sum % 11 == 0;
    }

    /// <summary>The ISBN-13 for a valid ISBN-10 (prefix 978, new check digit).</summary>
    public static string ToIsbn13(string isbn10)
    {
        var first12 = "978" + isbn10[..9];
        var sum = 0;
        for (var i = 0; i < 12; i++)
        {
            sum += (first12[i] - '0') * (i % 2 == 0 ? 1 : 3);
        }

        return first12 + (char)('0' + ((10 - (sum % 10)) % 10));
    }

    /// <summary>
    /// All valid ISBN-13 in the text, in order of appearance, without repeats. An ISBN-10 counts only
    /// if the word ISBN stands in front of it: without it, any ten digits with a fitting check digit would match.
    /// </summary>
    public static IReadOnlyList<string> FindAll(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var found = new List<string>();
        foreach (Match match in Candidate().Matches(NormalizeText(text)))
        {
            var digits = new string([.. match.Groups["num"].Value.Where(c => char.IsAsciiDigit(c) || c is 'X' or 'x')]);
            string? isbn = null;
            if (digits.Length == 13 && IsValid13(digits))
            {
                isbn = digits;
            }
            else if (digits.Length == 10 && match.Groups["prefix"].Success && IsValid10(digits))
            {
                isbn = ToIsbn13(digits);
            }

            if (isbn is not null && !found.Contains(isbn))
            {
                found.Add(isbn);
            }
        }

        return found;
    }

    /// <summary>The first ISBN of <see cref="FindAll"/>, or null.</summary>
    public static string? Find(string text) => FindAll(text) is [var first, ..] ? first : null;

    /// <summary>
    /// The ISBN-13 of a metadata identifier such as <c>urn:isbn:9780000000019</c> or <c>978-0-00-000002-6</c>;
    /// null if the identifier is anything else. Other URNs are rejected on purpose: the digits of a
    /// <c>urn:uuid:</c> identifier have a valid ISBN-13 check digit in one of ten cases (ADR 0005, E5).
    /// </summary>
    public static string? FromIdentifier(string identifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        var s = identifier.Trim();
        if (s.StartsWith("urn:isbn:", StringComparison.OrdinalIgnoreCase))
        {
            s = s["urn:isbn:".Length..];
        }
        else if (s.StartsWith("isbn:", StringComparison.OrdinalIgnoreCase))
        {
            s = s["isbn:".Length..];
        }

        s = NormalizeText(s).Trim();
        if (s.Length == 0 || !s.All(c => char.IsAsciiDigit(c) || c is 'X' or 'x' or '-' or ' '))
        {
            return null;
        }

        var digits = new string([.. s.Where(c => c is not ('-' or ' '))]);
        return digits.Length == 13 && IsValid13(digits) ? digits
            : digits.Length == 10 && IsValid10(digits) ? ToIsbn13(digits)
            : null;
    }

    /// <summary>NFKC (full-width digits, ligatures) and every dash variant to a plain hyphen.</summary>
    private static string NormalizeText(string text)
    {
        var normalized = text.Normalize(NormalizationForm.FormKC);
        var builder = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            builder.Append(c is >= '‐' and <= '―' or '−' ? '-' : c);
        }

        return builder.ToString();
    }
}
