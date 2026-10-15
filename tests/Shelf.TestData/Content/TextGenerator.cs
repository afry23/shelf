using System.Text;
using Shelf.TestData.Random;

namespace Shelf.TestData.Content;

internal sealed record Chapter(string Title, IReadOnlyList<string> Paragraphs);

internal sealed record TextDocument(IReadOnlyList<Chapter> Chapters)
{
    public IEnumerable<string> Paragraphs => Chapters.SelectMany(c => c.Paragraphs);

    public int WordCount => Paragraphs.Sum(p => CountWords(p));

    internal static int CountWords(string text) =>
        text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
}

/// <summary>
/// Pseudo-text from Zipf-weighted vocabulary words. Weights are integers (<c>floor(10^9 / rank)</c>) and the
/// sampling table is cumulative, so no floating point or libm result enters a decision.
/// </summary>
internal static class TextGenerator
{
    private const int FunctionWords = 120;
    private static readonly Lazy<long[]> CumulativeEnglish = new(() => BuildTable(Vocabulary.Words(Languages.English).Count));
    private static readonly Lazy<long[]> CumulativeGerman = new(() => BuildTable(Vocabulary.Words(Languages.German).Count));

    /// <summary>A document with exactly <paramref name="words"/> words in 5 to 12 chapters (fewer if there are fewer paragraphs).</summary>
    public static TextDocument Generate(string language, ulong seed, int words)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(words, 1);
        var rng = new Xoshiro256StarStar(seed);
        var chapterCount = 5 + rng.NextInt(8);
        var paragraphs = new List<string>();
        var remaining = words;
        while (remaining > 0)
        {
            var size = Math.Min(remaining, 40 + rng.NextInt(81));
            paragraphs.Add(Paragraph(language, rng, size));
            remaining -= size;
        }

        chapterCount = Math.Min(chapterCount, paragraphs.Count);
        var chapters = new List<Chapter>(chapterCount);
        for (var i = 0; i < chapterCount; i++)
        {
            var from = i * paragraphs.Count / chapterCount;
            var to = (i + 1) * paragraphs.Count / chapterCount;
            chapters.Add(new Chapter(ChapterTitle(language, rng), paragraphs.GetRange(from, to - from)));
        }

        return new TextDocument(chapters);
    }

    /// <summary>Copy of <paramref name="source"/> in which about <paramref name="percent"/> % of the paragraphs are replaced (same word counts).</summary>
    public static TextDocument Edition(TextDocument source, string language, ulong seed, int percent = 15)
    {
        var rng = new Xoshiro256StarStar(seed);
        var chapters = new List<Chapter>(source.Chapters.Count);
        foreach (var chapter in source.Chapters)
        {
            var paragraphs = new List<string>(chapter.Paragraphs.Count);
            foreach (var paragraph in chapter.Paragraphs)
            {
                paragraphs.Add(rng.NextInt(100) < percent
                    ? Paragraph(language, rng, TextDocument.CountWords(paragraph))
                    : paragraph);
            }

            chapters.Add(new Chapter(chapter.Title, paragraphs));
        }

        return new TextDocument(chapters);
    }

    public static string Capitalize(string word) =>
        word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word[1..];

    private static string ChapterTitle(string language, Xoshiro256StarStar rng)
    {
        var vocabulary = Vocabulary.Words(language);
        var count = 2 + rng.NextInt(2);
        var parts = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            parts.Add(Capitalize(vocabulary[FunctionWords + rng.NextInt(vocabulary.Count - FunctionWords)]));
        }

        return string.Join(' ', parts);
    }

    private static string Paragraph(string language, Xoshiro256StarStar rng, int words)
    {
        var vocabulary = Vocabulary.Words(language);
        var table = language == Languages.German ? CumulativeGerman.Value : CumulativeEnglish.Value;
        var builder = new StringBuilder(words * 8);
        var left = words;
        while (left > 0)
        {
            var length = Math.Min(left, 8 + rng.NextInt(11));
            for (var i = 0; i < length; i++)
            {
                var word = vocabulary[Sample(table, rng)];
                if (i == 0)
                {
                    if (builder.Length > 0)
                    {
                        builder.Append(' ');
                    }

                    word = Capitalize(word);
                }
                else
                {
                    builder.Append(' ');
                }

                builder.Append(word);
            }

            builder.Append('.');
            left -= length;
        }

        return builder.ToString();
    }

    private static int Sample(long[] cumulative, Xoshiro256StarStar rng)
    {
        var r = rng.NextInt64(cumulative[^1]);
        var low = 0;
        var high = cumulative.Length - 1;
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (cumulative[middle] > r)
            {
                high = middle;
            }
            else
            {
                low = middle + 1;
            }
        }

        return low;
    }

    private static long[] BuildTable(int size)
    {
        var table = new long[size];
        long sum = 0;
        for (var rank = 1; rank <= size; rank++)
        {
            sum += 1_000_000_000L / rank;
            table[rank - 1] = sum;
        }

        return table;
    }
}
