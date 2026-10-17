using System.Text.RegularExpressions;

namespace Shelf.Extraction;

internal static partial class TextHelpers
{
    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();

    /// <summary>Runs of white space become one space; null if nothing is left.</summary>
    public static string? Clean(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var cleaned = Whitespace().Replace(value, " ").Trim();
        return cleaned.Length == 0 ? null : cleaned;
    }

    /// <summary>The year at the start of a date such as <c>2017-05-01</c>, if it can be a publication year.</summary>
    public static int? YearOf(string? date)
    {
        if (date is null || date.Length < 4 || !int.TryParse(date.AsSpan(0, 4), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var year))
        {
            return null;
        }

        return year is >= 1000 and <= 2099 ? year : null;
    }
}
