using System.Globalization;

namespace Shelf.TestData.Content;

/// <summary>ISBN-13 helpers. Generated ISBNs carry valid check digits and may collide with real books (slice plan Q11).</summary>
internal static class Isbn
{
    /// <summary>Check digit for the first 12 digits.</summary>
    public static int CheckDigit(string first12)
    {
        if (first12.Length != 12)
        {
            throw new ArgumentException("Exactly 12 digits are required.", nameof(first12));
        }

        var sum = 0;
        for (var i = 0; i < 12; i++)
        {
            var digit = first12[i] - '0';
            if (digit is < 0 or > 9)
            {
                throw new ArgumentException("Only digits are allowed.", nameof(first12));
            }

            sum += digit * (i % 2 == 0 ? 1 : 3);
        }

        return (10 - (sum % 10)) % 10;
    }

    public static string Complete(string first12) =>
        first12 + CheckDigit(first12).ToString(CultureInfo.InvariantCulture);

    public static bool IsValid(string isbn13) =>
        isbn13.Length == 13 && isbn13.All(char.IsAsciiDigit) && CheckDigit(isbn13[..12]) == isbn13[12] - '0';

    /// <summary>"9780000000019" to "978-0-00-000001-9" (fixed hyphen layout of the derived fixtures, registrant group 0).</summary>
    public static string Hyphenate(string isbn13)
    {
        if (!IsValid(isbn13))
        {
            throw new ArgumentException("Not a valid ISBN-13.", nameof(isbn13));
        }

        return string.Join('-', isbn13[..3], isbn13[3..4], isbn13[4..6], isbn13[6..12], isbn13[12..]);
    }
}
