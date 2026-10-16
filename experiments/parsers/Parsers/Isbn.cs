using System.Text;
using System.Text.RegularExpressions;

namespace Proto;

/// <summary>ISBN scan (plan 3.4): NFKC, dash normalization, regex candidates, check digits; first valid hit wins.</summary>
public static partial class Isbn
{
    [GeneratedRegex(@"(?<![0-9])(?<prefix>ISBN(?:-1[03])?:?\s*)?(?<num>(?:97[89][-\s]?)?(?:[0-9][-\s]?){9}[0-9Xx])(?![0-9])", RegexOptions.CultureInvariant)]
    private static partial Regex Candidate();

    public static string Normalize(string text)
    {
        var s = text.Normalize(NormalizationForm.FormKC);
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            sb.Append(c is >= '‐' and <= '―' or '−' ? '-' : c);
        }

        return sb.ToString();
    }

    public static bool ValidIsbn13(string d)
    {
        if (d.Length != 13 || !d.All(char.IsAsciiDigit))
        {
            return false;
        }

        var sum = 0;
        for (var i = 0; i < 13; i++)
        {
            sum += (d[i] - '0') * (i % 2 == 0 ? 1 : 3);
        }

        return sum % 10 == 0;
    }

    public static bool ValidIsbn10(string d)
    {
        if (d.Length != 10 || !d[..9].All(char.IsAsciiDigit) || !(char.IsAsciiDigit(d[9]) || d[9] is 'X' or 'x'))
        {
            return false;
        }

        var sum = 0;
        for (var i = 0; i < 10; i++)
        {
            var v = d[i] is 'X' or 'x' ? 10 : d[i] - '0';
            sum += (10 - i) * v;
        }

        return sum % 11 == 0;
    }

    public static char CheckDigit13(string first12)
    {
        var sum = 0;
        for (var i = 0; i < 12; i++)
        {
            sum += (first12[i] - '0') * (i % 2 == 0 ? 1 : 3);
        }

        return (char)('0' + ((10 - (sum % 10)) % 10));
    }

    public static string ToIsbn13(string isbn10)
    {
        var first12 = "978" + isbn10[..9];
        return first12 + CheckDigit13(first12);
    }

    /// <summary>All valid ISBN-13 values in order of appearance (distinct). ISBN-10 is accepted only with an ISBN prefix (Q6).</summary>
    public static List<string> FindAll(string text)
    {
        var result = new List<string>();
        foreach (Match m in Candidate().Matches(Normalize(text)))
        {
            var digits = new string([.. m.Groups["num"].Value.Where(c => char.IsAsciiDigit(c) || c is 'X' or 'x')]);
            string? hit = null;
            if (digits.Length == 13 && ValidIsbn13(digits))
            {
                hit = digits;
            }
            else if (digits.Length == 10 && m.Groups["prefix"].Success && ValidIsbn10(digits))
            {
                hit = ToIsbn13(digits);
            }

            if (hit is not null && !result.Contains(hit))
            {
                result.Add(hit);
            }
        }

        return result;
    }

    public static string? Find(string text) => FindAll(text) is { Count: > 0 } l ? l[0] : null;

    /// <summary>
    /// Normalizes an OPF identifier such as "urn:isbn:9780000000019" or "978-0-00-000002-6"; null if it is not exclusively an ISBN.
    /// Other URNs are rejected: the digits of a "urn:uuid:" identifier form a valid ISBN-13 check digit in 1 of 10 cases.
    /// </summary>
    public static string? FromIdentifier(string identifier)
    {
        var s = identifier.Trim();
        if (s.StartsWith("urn:isbn:", StringComparison.OrdinalIgnoreCase))
        {
            s = s["urn:isbn:".Length..];
        }
        else if (s.StartsWith("isbn:", StringComparison.OrdinalIgnoreCase))
        {
            s = s["isbn:".Length..];
        }

        s = Normalize(s).Trim();
        if (s.Length == 0 || !s.All(c => char.IsAsciiDigit(c) || c is 'X' or 'x' or '-' or ' '))
        {
            return null;
        }

        var digits = new string([.. s.Where(c => c is not ('-' or ' '))]);
        return digits.Length == 13 && ValidIsbn13(digits) ? digits
            : digits.Length == 10 && ValidIsbn10(digits) ? ToIsbn13(digits)
            : null;
    }

    public static int Check(TextWriter w)
    {
        var rows = new Rows(w);
        rows.Gate("I1", Find("ISBN 978-0-00-000001-9") == "9780000000019");
        rows.Gate("I2", Find("ISBN-13: 9780000000026") == "9780000000026");
        rows.Gate("I3", Find("ISBN 0-306-40615-2") == "9780306406157");
        rows.Gate("I4", Find("ISBN 978‐0‐306‐40615‐7") == "9780306406157");
        rows.Gate("I5", Find("ISBN 978-0-306-40615-8") is null);
        rows.Observe("I6", "call 0306406152 now -> " + (Find("call 0306406152 now") ?? "none") + " (ISBN-10 without ISBN prefix is rejected; with prefix: " + Find("call ISBN 0306406152 now") + ")");
        rows.Gate("I7", Find("12345678901234") is null);
        rows.Gate("I-convert", ToIsbn13("0306406152") == "9780306406157");
        rows.Gate("I-checkdigit", CheckDigit13("978000000001") == '9');
        rows.Gate("I-identifier", FromIdentifier("urn:isbn:9780000000019") == "9780000000019" && FromIdentifier("9780000000026") == "9780000000026" && FromIdentifier("urn:uuid:1234") is null && FromIdentifier("urn:uuid:9b6ffe1d-f194-4cc9-bf3f-fdd005afba9d") is null && FromIdentifier("978-0-00-000002-6") == "9780000000026");
        return rows.Finish();
    }
}
