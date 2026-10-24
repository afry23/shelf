namespace Shelf.Extraction;

/// <summary>
/// Removes what a print layout repeats on the pages of a PDF and that is not running text: page numbers at the
/// start or end of a page, and the running head and foot (book title, chapter title). Books often alternate the
/// head between left and right pages, so odd and even pages are looked at separately. Without it the word triples
/// that span a page break are different from the same text in an EPUB.
/// </summary>
internal static class RunningHeads
{
    /// <summary>Fewer pages in a group than this: nothing is removed (a short text has no running head to find).</summary>
    private const int MinPages = 6;

    /// <summary>A head longer than this is a paragraph that repeats, not a head.</summary>
    private const int MaxWords = 12;

    public static void Strip(List<(int Number, List<string> Words)> pages)
    {
        foreach (var (_, words) in pages)
        {
            if (words.Count > 0 && IsPageNumber(words[0]))
            {
                words.RemoveAt(0);
            }

            if (words.Count > 0 && IsPageNumber(words[^1]))
            {
                words.RemoveAt(words.Count - 1);
            }
        }

        foreach (var group in pages.GroupBy(p => p.Number % 2))
        {
            var members = group.Select(p => p.Words).ToList();
            if (members.Count < MinPages)
            {
                continue;
            }

            var head = Common(members, fromStart: true);
            var foot = Common(members, fromStart: false);
            foreach (var words in members)
            {
                // Only pages that really start (end) with it lose it; the others are not touched.
                if (head.Length > 0 && words.Count > head.Length && Key(words, head.Length, fromStart: true) == head.Key)
                {
                    words.RemoveRange(0, head.Length);
                }

                if (foot.Length > 0 && words.Count > foot.Length && Key(words, foot.Length, fromStart: false) == foot.Key)
                {
                    words.RemoveRange(words.Count - foot.Length, foot.Length);
                }
            }
        }
    }

    private static bool IsPageNumber(string word) => word.Length <= 4 && word.All(char.IsDigit);

    /// <summary>The longest run of words at the start (end) that at least 60 percent of the pages have in common.</summary>
    private static (int Length, string Key) Common(List<List<string>> pages, bool fromStart)
    {
        var best = (Length: 0, Key: string.Empty);
        for (var k = 1; k <= MaxWords; k++)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var words in pages.Where(w => w.Count > k))
            {
                var key = Key(words, k, fromStart);
                counts[key] = counts.GetValueOrDefault(key) + 1;
            }

            if (counts.Count == 0)
            {
                break;
            }

            var top = counts.MaxBy(kv => kv.Value);
            if (top.Value * 10 < pages.Count * 6)
            {
                break;
            }

            best = (k, top.Key);
        }

        return best;
    }

    private static string Key(List<string> words, int length, bool fromStart) =>
        string.Join(' ', fromStart ? words.Take(length) : words.Skip(words.Count - length)).ToLowerInvariant();
}
