using Shelf.Core.Model;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Outline;
using VersOne.Epub;

namespace Shelf.Extraction;

/// <summary>An entry of the table of contents. <paramref name="Level"/> starts at 1; <paramref name="Locator"/> is null when the entry points nowhere shelf can name.</summary>
public sealed record TocEntry(string Title, int Level, string? Locator);

/// <summary>
/// The table of contents of a file (MCP tool <c>get_book</c>, resource <c>shelf://book/{id}/toc</c>): the navigation of an
/// EPUB or the outline of a PDF. Read when asked, not stored in the index. A file without one, or one that cannot be read,
/// gives an empty list: the table of contents is a convenience, never a reason to fail.
/// </summary>
public static class TocExtractor
{
    public static IReadOnlyList<TocEntry> Read(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        try
        {
            return Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".epub" => ReadEpub(path),
                ".pdf" => ReadPdf(path),
                _ => [],
            };
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return [];
        }
    }

    private static List<TocEntry> ReadEpub(string path)
    {
        using var book = EpubReader.OpenBookAsync(path).GetAwaiter().GetResult();
        var order = book.GetReadingOrderAsync().GetAwaiter().GetResult();
        var spine = book.Schema.Package.Spine.Items;
        var chapters = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < order.Count && i < spine.Count; i++)
        {
            chapters.TryAdd(order[i].FilePath, Locators.EpubChapter(i, spine[i].IdRef));
        }

        var entries = new List<TocEntry>();
        Walk(book.GetNavigationAsync().GetAwaiter().GetResult() ?? [], 1, chapters, entries);
        return entries;
    }

    private static void Walk(IEnumerable<EpubNavigationItemRef> items, int level, Dictionary<string, string> chapters, List<TocEntry> entries)
    {
        foreach (var item in items)
        {
            var title = TextHelpers.Clean(item.Title);
            if (title is not null)
            {
                var file = item.Link?.ContentFilePath;
                entries.Add(new TocEntry(title, level, file is not null && chapters.TryGetValue(file, out var locator) ? locator : null));
            }

            Walk(item.NestedItems ?? [], level + 1, chapters, entries);
        }
    }

    private static List<TocEntry> ReadPdf(string path)
    {
        using var document = PdfDocument.Open(path, new ParsingOptions { UseLenientParsing = true });
        var entries = new List<TocEntry>();
        if (document.TryGetBookmarks(out var bookmarks))
        {
            foreach (var root in bookmarks.Roots)
            {
                Walk(root, 1, entries);
            }
        }

        return entries;
    }

    private static void Walk(BookmarkNode node, int level, List<TocEntry> entries)
    {
        var title = TextHelpers.Clean(node.Title);
        if (title is not null)
        {
            entries.Add(new TocEntry(title, level, node is DocumentBookmarkNode { PageNumber: >= 1 } document ? Locators.PdfPage(document.PageNumber) : null));
        }

        foreach (var child in node.Children)
        {
            Walk(child, level + 1, entries);
        }
    }
}
