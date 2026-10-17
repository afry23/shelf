using System.Globalization;
using System.Text;

namespace Shelf.Core.Model;

/// <summary>
/// Where a piece of text sits inside a file (F-SRC-5): <c>page=N</c> for a PDF page (1-based) and an
/// EPUB CFI of the spine item for a chapter. The strings are stored in the index and used by the readers.
/// </summary>
public static class Locators
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

    public static string PdfPage(int pageNumber)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageNumber, 1);
        return "page=" + pageNumber.ToString(CultureInfo.InvariantCulture);
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
