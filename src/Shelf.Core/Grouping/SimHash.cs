using System.Numerics;
using System.Text;

namespace Shelf.Core.Grouping;

/// <summary>
/// A 64 bit fingerprint of the running text of a file (F-GRP-1, step 3): the same book as EPUB and as PDF gets
/// fingerprints a few bits apart. Over the first 5000 words, each distinct word weighted by 1 + ln(count).
/// Case, accents and punctuation do not matter. Short texts (a scan, a leaflet) have no fingerprint.
/// </summary>
public static class SimHash
{
    public const int MaxWords = 5000;
    public const int MinWords = 200;

    public static int Distance(ulong first, ulong second) => BitOperations.PopCount(first ^ second);

    /// <summary>The fingerprint of the text pieces taken together, or null if there are fewer than <see cref="MinWords"/> words.</summary>
    public static ulong? Compute(IEnumerable<string> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var words = 0;
        foreach (var segment in segments)
        {
            foreach (var word in Words(segment))
            {
                counts[word] = counts.GetValueOrDefault(word) + 1;
                if (++words == MaxWords)
                {
                    return Fingerprint(counts, words);
                }
            }
        }

        return Fingerprint(counts, words);
    }

    private static ulong? Fingerprint(Dictionary<string, int> counts, int words)
    {
        if (words < MinWords)
        {
            return null;
        }

        Span<double> sums = stackalloc double[64];
        foreach (var (word, count) in counts)
        {
            var hash = Hash(word);
            var weight = 1 + Math.Log(count);
            for (var bit = 0; bit < 64; bit++)
            {
                sums[bit] += (hash >> bit & 1) == 1 ? weight : -weight;
            }
        }

        ulong result = 0;
        for (var bit = 0; bit < 64; bit++)
        {
            if (sums[bit] > 0)
            {
                result |= 1UL << bit;
            }
        }

        return result;
    }

    /// <summary>Words of a text: runs of letters and digits, folded.</summary>
    private static IEnumerable<string> Words(string text)
    {
        var word = new StringBuilder();
        foreach (var c in text)
        {
            if (char.IsLetterOrDigit(c))
            {
                if (c < 128)
                {
                    word.Append(char.ToLowerInvariant(c));
                }
                else
                {
                    TextFolding.AppendFolded(word, c);
                }
            }
            else if (word.Length > 0)
            {
                yield return word.ToString();
                word.Clear();
            }
        }

        if (word.Length > 0)
        {
            yield return word.ToString();
        }
    }

    /// <summary>FNV-1a over the characters, then a mixing step so that every output bit depends on every input bit.</summary>
    private static ulong Hash(string word)
    {
        ulong hash = 14695981039346656037UL;
        foreach (var c in word)
        {
            hash = (hash ^ c) * 1099511628211UL;
        }

        hash = (hash ^ (hash >> 30)) * 0xBF58476D1CE4E5B9UL;
        hash = (hash ^ (hash >> 27)) * 0x94D049BB133111EBUL;
        return hash ^ (hash >> 31);
    }
}
