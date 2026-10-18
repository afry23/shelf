using System.Numerics;
using System.Text;

namespace Shelf.Core.Grouping;

/// <summary>
/// A 64 bit fingerprint of the running text of a file (F-GRP-1, step 3): the same book as EPUB and as PDF gets
/// fingerprints a few bits apart, different books far apart. Built from the distinct runs of three consecutive
/// words in the first 5000 words, each counted once. Case, accents and punctuation do not matter. Short texts
/// (a scan, a leaflet) have no fingerprint.
/// </summary>
/// <remarks>
/// Why word triples and not single words: books of one language and field share most of their words, so the
/// fingerprints of single words were within 3 bits for thousands of pairs of different works of the generated
/// reference library (measured). Triples of words are specific to a text: different works were never closer than
/// 6 bits, the files of one work never farther than 6. The price is sensitivity to changes between formats
/// (every changed word touches three triples). The algorithm is stored in the index; changing it means every
/// stored fingerprint has to be computed again.
/// </remarks>
public static class SimHash
{
    public const int MaxWords = 5000;
    public const int MinWords = 200;
    public const int ShingleSize = 3;

    public static int Distance(ulong first, ulong second) => BitOperations.PopCount(first ^ second);

    /// <summary>The fingerprint of the text pieces taken together, or null if there are fewer than <see cref="MinWords"/> words.</summary>
    public static ulong? Compute(IEnumerable<string> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        var words = new List<string>();
        foreach (var segment in segments)
        {
            foreach (var word in Words(segment))
            {
                words.Add(word);
                if (words.Count == MaxWords)
                {
                    return Fingerprint(words);
                }
            }
        }

        return Fingerprint(words);
    }

    private static ulong? Fingerprint(List<string> words)
    {
        if (words.Count < MinWords)
        {
            return null;
        }

        // Each distinct triple votes once: a running header repeated on every page must not outweigh the text.
        var shingles = new HashSet<ulong>();
        for (var i = 0; i + ShingleSize <= words.Count; i++)
        {
            shingles.Add(Hash(words[i], words[i + 1], words[i + 2]));
        }

        Span<int> votes = stackalloc int[64];
        foreach (var hash in shingles)
        {
            for (var bit = 0; bit < 64; bit++)
            {
                votes[bit] += (hash >> bit & 1) == 1 ? 1 : -1;
            }
        }

        ulong result = 0;
        for (var bit = 0; bit < 64; bit++)
        {
            if (votes[bit] > 0)
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

    /// <summary>FNV-1a over the characters of three words (with a separator), then a mixing step so every output bit depends on every input bit.</summary>
    private static ulong Hash(string first, string second, string third)
    {
        ulong hash = 14695981039346656037UL;
        foreach (var word in new[] { first, second, third })
        {
            foreach (var c in word)
            {
                hash = (hash ^ c) * 1099511628211UL;
            }

            hash = (hash ^ ' ') * 1099511628211UL;
        }

        hash = (hash ^ (hash >> 30)) * 0xBF58476D1CE4E5B9UL;
        hash = (hash ^ (hash >> 27)) * 0x94D049BB133111EBUL;
        return hash ^ (hash >> 31);
    }
}
