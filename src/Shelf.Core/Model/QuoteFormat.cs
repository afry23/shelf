using System.Text;
using System.Text.RegularExpressions;

namespace Shelf.Core.Model;

/// <summary>
/// A passage as a quote with its citation (F-ANN-8): a Markdown block quote, then a Pandoc citation with the place,
/// <c>[@alander2021event, p. 12]</c>. The cite key must be in the bibliography of the text that the quote is pasted into
/// (<c>shelf export</c> gives it).
/// </summary>
public static partial class QuoteFormat
{
    /// <summary>The longest passage that is quoted, in characters (as for a highlight).</summary>
    public const int MaxQuoteLength = 20_000;

    /// <summary>The Markdown text. <paramref name="locator"/> is a place in a file or null; a place that is none of a page or a section is left out.</summary>
    /// <exception cref="ArgumentException">The cite key or the quote is empty.</exception>
    public static string Render(string citeKey, string quote, string? locator)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(citeKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(quote);

        var lines = quote.ReplaceLineEndings("\n").Split('\n').Select(l => Whitespace().Replace(l, " ").Trim()).ToList();
        while (lines.Count > 0 && lines[^1].Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }

        var text = new StringBuilder();
        foreach (var line in lines.SkipWhile(l => l.Length == 0))
        {
            text.Append(line.Length == 0 ? ">" : "> " + line).Append('\n');
        }

        text.Append("\n[@").Append(citeKey);
        if (Pandoc(locator) is { } place)
        {
            text.Append(", ").Append(place);
        }

        return text.Append(']').ToString();
    }

    /// <summary>The place for people ("page 12", "section 3"), or null if the locator is none of both.</summary>
    public static string? Place(string? locator)
    {
        if (string.IsNullOrEmpty(locator))
        {
            return null;
        }

        var place = Locators.Describe(locator);
        return place.StartsWith("page ", StringComparison.Ordinal) || place.StartsWith("section ", StringComparison.Ordinal) ? place : null;
    }

    private static string? Pandoc(string? locator) => Place(locator) switch
    {
        { } place when place.StartsWith("page ", StringComparison.Ordinal) => "p. " + place[5..],
        { } place => "sec. " + place[8..],
        null => null,
    };

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
