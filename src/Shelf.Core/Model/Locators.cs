using System.Globalization;
using System.Text;

namespace Shelf.Core.Model;

/// <summary>
/// Where a piece of text sits inside a file (F-SRC-5): <c>page=N</c> for a PDF page (1-based) and an
/// EPUB CFI of the spine item for a chapter. The strings are stored in the index and used by the readers.
/// </summary>
public static partial class Locators
{
    /// <summary>
    /// The CFI of a spine item: the package is step 6 of the root, the spine item number n (0-based) is step
    /// 2(n+1), and the id of the item is added in brackets, with the CFI escape character before ^ [ ] ( ) , ; =.
    /// </summary>
    public static string EpubChapter(int spineIndex, string idref)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(spineIndex);
        ArgumentException.ThrowIfNullOrEmpty(idref);
        var id = new StringBuilder(idref.Length);
        foreach (var c in idref)
        {
            if (c is '^' or '[' or ']' or '(' or ')' or ',' or ';' or '=')
            {
                id.Append('^');
            }

            id.Append(c);
        }

        return $"epubcfi(/6/{2 * (spineIndex + 1)}[{id}]!)";
    }

    /// <summary>Why this location is none of a file of this format (<c>pdf</c> or <c>epub</c>), or null.</summary>
    public static string? Problem(string format, string locator)
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(locator);
        return format switch
        {
            "pdf" => PageLocator().IsMatch(locator)
                ? null
                : "locator of a PDF must be page=N, with N a page number from 1",
            "epub" => locator.Length <= 2_000 && locator.StartsWith("epubcfi(", StringComparison.Ordinal) && locator.EndsWith(')')
                ? null
                : "locator of an EPUB must be an epubcfi(...)",
            "md" => HeadingLocator().IsMatch(locator)
                ? null
                : "locator of a Markdown file must be heading=SLUG",
            _ => $"files of the format {format} have no reading position",
        };
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"^heading=[\p{L}\p{N}-]{1,200}$")]
    private static partial System.Text.RegularExpressions.Regex HeadingLocator();

    [System.Text.RegularExpressions.GeneratedRegex("^page=[1-9][0-9]{0,8}$")]
    private static partial System.Text.RegularExpressions.Regex PageLocator();

    public static string PdfPage(int pageNumber)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageNumber, 1);
        return "page=" + pageNumber.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// A location for people: "page 212" for a PDF page, "section 3" for the third spine item of an EPUB (the
    /// CFI step 6 is the third item); anything else as it is. A section is not always a chapter: a cover page counts.
    /// </summary>
    public static string Describe(string locator)
    {
        ArgumentNullException.ThrowIfNull(locator);
        if (TryParsePdfPage(locator, out var page))
        {
            return "page " + page.ToString(CultureInfo.InvariantCulture);
        }

        if (locator.StartsWith("heading=", StringComparison.Ordinal))
        {
            var slug = locator["heading=".Length..];
            return slug == "start" ? "start" : "section \"" + slug.Replace('-', ' ') + "\"";
        }

        const string prefix = "epubcfi(/6/";
        if (locator.StartsWith(prefix, StringComparison.Ordinal))
        {
            var digits = locator.AsSpan(prefix.Length);
            var end = 0;
            while (end < digits.Length && char.IsAsciiDigit(digits[end]))
            {
                end++;
            }

            if (end > 0 && int.TryParse(digits[..end], NumberStyles.None, CultureInfo.InvariantCulture, out var step) && step >= 2)
            {
                return "section " + (step / 2).ToString(CultureInfo.InvariantCulture);
            }
        }

        return locator;
    }

    /// <summary>True for <c>page=N</c> with N of 1 or more.</summary>
    public static bool TryParsePdfPage(string? locator, out int pageNumber)
    {
        pageNumber = 0;
        return locator is not null
            && locator.StartsWith("page=", StringComparison.Ordinal)
            && int.TryParse(locator.AsSpan(5), NumberStyles.None, CultureInfo.InvariantCulture, out pageNumber)
            && pageNumber >= 1;
    }
}
