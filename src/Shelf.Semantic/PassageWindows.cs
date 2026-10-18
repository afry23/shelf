namespace Shelf.Semantic;

/// <summary>
/// Cuts the text of a chunk into overlapping windows that fit the 512 tokens of the model (F-SEM-2). A chunk of the
/// full-text index has up to 600 words, which is more than the model reads; each window of 280 words (about 400 to
/// 500 tokens) starts 240 words after the one before, so neighbours share 40 words. The location of a window is the
/// location of its chunk.
/// </summary>
public static class PassageWindows
{
    public const int WindowWords = 280;
    public const int StrideWords = 240;

    /// <summary>The most windows per chunk; a vector's key is <c>chunkId * MaxWindows + part</c>.</summary>
    public const int MaxWindows = 8;

    public static IReadOnlyList<string> Split(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length <= WindowWords)
        {
            return [text];
        }

        var windows = new List<string>();
        for (var start = 0; windows.Count < MaxWindows; start += StrideWords)
        {
            var end = Math.Min(start + WindowWords, words.Length);
            windows.Add(string.Join(' ', words, start, end - start));
            if (end == words.Length)
            {
                break;
            }
        }

        return windows;
    }

    public static long KeyOf(long chunkId, int part) => (chunkId * MaxWindows) + part;

    public static long ChunkOf(long key) => key / MaxWindows;
}
