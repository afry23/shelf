namespace Shelf.Extraction;

/// <summary>A piece of text for the full-text index (and for embeddings, unless <paramref name="Kind"/> says it is no prose) with the location it came from.</summary>
public sealed record TextChunk(int Ordinal, string Locator, string Text, ContentKind Kind = ContentKind.Body);

/// <summary>
/// Cuts the segments of a file (pages, chapters) into chunks of about 500 words. A chunk never crosses a
/// segment border, so its locator is exact. A segment of up to 600 words is one chunk; a longer one is cut into
/// equal parts of about 500 words. White space is collapsed to single spaces.
/// </summary>
public static class TextChunker
{
    public const int TargetWords = 500;
    public const int MaxWords = 600;

    public static IReadOnlyList<TextChunk> Chunk(IEnumerable<TextSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        var chunks = new List<TextChunk>();
        foreach (var segment in segments)
        {
            var words = segment.Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                continue;
            }

            var parts = words.Length <= MaxWords ? 1 : (words.Length + TargetWords - 1) / TargetWords;
            var size = (words.Length + parts - 1) / parts;
            for (var from = 0; from < words.Length; from += size)
            {
                var count = Math.Min(size, words.Length - from);
                var text = string.Join(' ', words, from, count);
                chunks.Add(new TextChunk(chunks.Count, segment.Locator, text, ContentClassifier.Classify(segment.Kind, text)));
            }
        }

        return chunks;
    }
}
