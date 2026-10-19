using System.Text;

namespace Shelf.Core.Search;

/// <summary>
/// Language-dependent stemming for German and English (F-SRC-6): English with Porter2, German with the Snowball
/// German algorithm. Words are expected in lower case; the result is lower case, too. Plain managed code, no
/// dependency, so it works under Native AOT.
/// </summary>
public static class Stemmer
{
    public const string English = "en";
    public const string German = "de";

    /// <summary>The stemmer for a language tag (<c>en</c>, <c>de-AT</c>, ...), or <c>null</c> if there is none.</summary>
    public static string? For(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return null;
        }

        var primary = language.AsSpan().Trim();
        var dash = primary.IndexOfAny('-', '_');
        if (dash >= 0)
        {
            primary = primary[..dash];
        }

        if (primary.Equals(English, StringComparison.OrdinalIgnoreCase) || primary.Equals("eng", StringComparison.OrdinalIgnoreCase))
        {
            return English;
        }

        return primary.Equals(German, StringComparison.OrdinalIgnoreCase) || primary.Equals("deu", StringComparison.OrdinalIgnoreCase) || primary.Equals("ger", StringComparison.OrdinalIgnoreCase)
            ? German
            : null;
    }

    /// <summary>Stems a word with the stemmer <see cref="English"/> or <see cref="German"/>; other values return the word.</summary>
    public static string Stem(string word, string? stemmer)
    {
        ArgumentNullException.ThrowIfNull(word);
        return stemmer switch
        {
            English => StemEnglish(word),
            German => StemGerman(word),
            _ => word,
        };
    }

    /// <summary>The words of a text (letters and digits), in lower case.</summary>
    public static IEnumerable<string> Words(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            yield break;
        }

        var start = -1;
        for (var i = 0; i <= text.Length; i++)
        {
            var inWord = i < text.Length && char.IsLetterOrDigit(text[i]);
            if (inWord && start < 0)
            {
                start = i;
            }
            else if (!inWord && start >= 0)
            {
                yield return text[start..i].ToLowerInvariant();
                start = -1;
            }
        }
    }

    // ---- English (Porter2) ----

    private static readonly Dictionary<string, string> EnglishExceptions = new(StringComparer.Ordinal)
    {
        ["skis"] = "ski", ["skies"] = "sky", ["dying"] = "die", ["lying"] = "lie", ["tying"] = "tie",
        ["idly"] = "idl", ["gently"] = "gentl", ["ugly"] = "ugli", ["early"] = "earli", ["only"] = "onli", ["singly"] = "singl",
        ["sky"] = "sky", ["news"] = "news", ["howe"] = "howe", ["atlas"] = "atlas", ["cosmos"] = "cosmos", ["bias"] = "bias", ["andes"] = "andes",
    };

    private static readonly HashSet<string> EnglishKeepAfterStep1A = new(StringComparer.Ordinal)
    {
        "inning", "outing", "canning", "herring", "earring", "proceed", "exceed", "succeed",
    };

    private static string StemEnglish(string word)
    {
        if (word.Length <= 2 || !IsAscii(word))
        {
            return word;
        }

        if (EnglishExceptions.TryGetValue(word, out var exception))
        {
            return exception;
        }

        var w = new StringBuilder(word);
        if (w[0] == '\'')
        {
            w.Remove(0, 1);
        }

        if (w.Length > 0 && w[0] == 'y')
        {
            w[0] = 'Y';
        }

        for (var i = 1; i < w.Length; i++)
        {
            if (w[i] == 'y' && IsEnglishVowel(w[i - 1]))
            {
                w[i] = 'Y';
            }
        }

        var (r1, _) = EnglishRegions(w.ToString());

        // Step 0
        RemoveLongest(w, "'s'", "'s", "'");

        // Step 1a
        var s = w.ToString();
        if (s.EndsWith("sses", StringComparison.Ordinal))
        {
            w.Length -= 2;
        }
        else if (s.EndsWith("ied", StringComparison.Ordinal) || s.EndsWith("ies", StringComparison.Ordinal))
        {
            w.Length -= s.Length - 3 > 1 ? 2 : 1;
        }
        else if (s.EndsWith('s') && !s.EndsWith("us", StringComparison.Ordinal) && !s.EndsWith("ss", StringComparison.Ordinal)
            && s.Length > 2 && s[..^2].Any(IsEnglishVowel))
        {
            w.Length--;
        }

        if (EnglishKeepAfterStep1A.Contains(w.ToString()))
        {
            return w.ToString();
        }

        // Step 1b
        s = w.ToString();
        var suffix = Longest(s, "eedly", "eed", "ingly", "edly", "ing", "ed");
        if (suffix is "eed" or "eedly")
        {
            if (s.Length - suffix.Length >= r1)
            {
                w.Length -= suffix.Length;
                w.Append("ee");
            }
        }
        else if (suffix is not null && s[..^suffix.Length].Any(IsEnglishVowel))
        {
            w.Length -= suffix.Length;
            s = w.ToString();
            if (s.EndsWith("at", StringComparison.Ordinal) || s.EndsWith("bl", StringComparison.Ordinal) || s.EndsWith("iz", StringComparison.Ordinal))
            {
                w.Append('e');
            }
            else if (s.Length >= 2 && s[^1] == s[^2] && "bdfgmnprt".Contains(s[^1], StringComparison.Ordinal))
            {
                w.Length--;
            }
            else if (EnglishRegions(s).R1 >= s.Length && EndsInShortSyllable(s))
            {
                w.Append('e');
            }
        }

        // Step 1c
        s = w.ToString();
        if (s.Length > 2 && (s[^1] is 'y' or 'Y') && !IsEnglishVowel(s[^2]))
        {
            w[^1] = 'i';
        }

        // The regions of the word as it is now.
        (r1, var r2) = EnglishRegions(w.ToString());

        // Step 2
        s = w.ToString();
        suffix = Longest(s, "ization", "ational", "fulness", "ousness", "iveness", "tional", "biliti", "lessli", "entli", "ation", "alism", "aliti", "ousli", "iviti", "fulli", "enci", "anci", "abli", "izer", "ator", "alli", "bli", "ogi", "li");
        if (suffix is not null && s.Length - suffix.Length >= r1)
        {
            var stem = s[..^suffix.Length];
            string? replacement = suffix switch
            {
                "tional" => "tion",
                "enci" => "ence",
                "anci" => "ance",
                "abli" => "able",
                "entli" => "ent",
                "izer" or "ization" => "ize",
                "ational" or "ation" or "ator" => "ate",
                "alism" or "aliti" or "alli" => "al",
                "fulness" => "ful",
                "ousli" or "ousness" => "ous",
                "iveness" or "iviti" => "ive",
                "biliti" or "bli" => "ble",
                "ogi" => stem.EndsWith('l') ? "og" : null,
                "fulli" => "ful",
                "lessli" => "less",
                "li" => stem.Length > 0 && "cdeghkmnrt".Contains(stem[^1], StringComparison.Ordinal) ? string.Empty : null,
                _ => null,
            };
            if (replacement is not null)
            {
                w.Length = stem.Length;
                w.Append(replacement);
            }
        }

        // Step 3
        s = w.ToString();
        suffix = Longest(s, "ational", "tional", "alize", "icate", "iciti", "ative", "ical", "ness", "ful");
        if (suffix is not null && s.Length - suffix.Length >= r1)
        {
            var stem = s[..^suffix.Length];
            switch (suffix)
            {
                case "tional":
                    w.Length = stem.Length;
                    w.Append("tion");
                    break;
                case "ational":
                    w.Length = stem.Length;
                    w.Append("ate");
                    break;
                case "alize":
                    w.Length = stem.Length;
                    w.Append("al");
                    break;
                case "icate" or "iciti" or "ical":
                    w.Length = stem.Length;
                    w.Append("ic");
                    break;
                case "ful" or "ness":
                    w.Length = stem.Length;
                    break;
                case "ative":
                    if (s.Length - suffix.Length >= r2)
                    {
                        w.Length = stem.Length;
                    }

                    break;
            }
        }

        // Step 4
        s = w.ToString();
        suffix = Longest(s, "ement", "ance", "ence", "able", "ible", "ment", "ant", "ent", "ism", "ate", "iti", "ous", "ive", "ize", "ion", "al", "er", "ic");
        if (suffix is not null && s.Length - suffix.Length >= r2)
        {
            var stem = s[..^suffix.Length];
            if (suffix != "ion" || (stem.Length > 0 && stem[^1] is 's' or 't'))
            {
                w.Length = stem.Length;
            }
        }

        // Step 5
        s = w.ToString();
        if (s.EndsWith('e'))
        {
            var stem = s[..^1];
            if (stem.Length >= r2 || (stem.Length >= r1 && !EndsInShortSyllable(stem)))
            {
                w.Length--;
            }
        }
        else if (s.EndsWith("ll", StringComparison.Ordinal) && s.Length - 1 >= r2)
        {
            w.Length--;
        }

        return w.ToString().Replace('Y', 'y');
    }

    private static bool IsAscii(string word) => word.All(c => c < 128);

    private static bool IsEnglishVowel(char c) => c is 'a' or 'e' or 'i' or 'o' or 'u' or 'y';

    /// <summary>Start indexes of R1 and R2 (the length of the word if the region is empty).</summary>
    private static (int R1, int R2) EnglishRegions(string word)
    {
        int Region(int from)
        {
            for (var i = Math.Max(from, 0) + 1; i < word.Length; i++)
            {
                if (!IsEnglishVowel(word[i]) && IsEnglishVowel(word[i - 1]))
                {
                    return i + 1;
                }
            }

            return word.Length;
        }

        int r1;
        if (word.StartsWith("gener", StringComparison.Ordinal) || word.StartsWith("arsen", StringComparison.Ordinal))
        {
            r1 = 5;
        }
        else if (word.StartsWith("commun", StringComparison.Ordinal))
        {
            r1 = 6;
        }
        else
        {
            r1 = Region(0);
        }

        return (r1, r1 >= word.Length ? word.Length : Region(r1));
    }

    private static bool EndsInShortSyllable(string word)
    {
        if (word.Length == 2)
        {
            return IsEnglishVowel(word[0]) && !IsEnglishVowel(word[1]);
        }

        if (word.Length < 3)
        {
            return false;
        }

        var a = word[^3];
        var b = word[^2];
        var c = word[^1];
        return !IsEnglishVowel(a) && IsEnglishVowel(b) && !IsEnglishVowel(c) && c is not ('w' or 'x' or 'Y');
    }

    private static void RemoveLongest(StringBuilder word, params string[] suffixes)
    {
        if (Longest(word.ToString(), suffixes) is { } suffix)
        {
            word.Length -= suffix.Length;
        }
    }

    /// <summary>The longest of the suffixes that the word ends with.</summary>
    private static string? Longest(string word, params string[] suffixes)
    {
        string? best = null;
        foreach (var suffix in suffixes)
        {
            if (word.EndsWith(suffix, StringComparison.Ordinal) && (best is null || suffix.Length > best.Length))
            {
                best = suffix;
            }
        }

        return best;
    }

    // ---- German (Snowball) ----

    private static bool IsGermanVowel(char c) => c is 'a' or 'e' or 'i' or 'o' or 'u' or 'y' or 'ä' or 'ö' or 'ü';

    private static string StemGerman(string word)
    {
        if (word.Length == 0)
        {
            return word;
        }

        var w = new StringBuilder(word.Replace("ß", "ss", StringComparison.Ordinal));
        for (var i = 1; i < w.Length - 1; i++)
        {
            if ((w[i] is 'u' or 'y') && IsGermanVowel(w[i - 1]) && IsGermanVowel(w[i + 1]))
            {
                w[i] = char.ToUpperInvariant(w[i]);
            }
        }

        int Region(string s, int from)
        {
            for (var i = Math.Max(from, 0) + 1; i < s.Length; i++)
            {
                if (!IsGermanVowel(s[i]) && IsGermanVowel(s[i - 1]))
                {
                    return i + 1;
                }
            }

            return s.Length;
        }

        var text = w.ToString();
        var r1 = Math.Max(Region(text, 0), 3);
        r1 = Math.Min(r1, text.Length);
        var r2 = r1 >= text.Length ? text.Length : Region(text, r1);

        // Step 1
        var s1 = w.ToString();
        var suffix = Longest(s1, "ern", "em", "er", "en", "es", "e", "s");
        if (suffix is not null && s1.Length - suffix.Length >= r1)
        {
            var stem = s1[..^suffix.Length];
            switch (suffix)
            {
                case "em" or "er" or "ern":
                    w.Length = stem.Length;
                    break;
                case "e" or "en" or "es":
                    w.Length = stem.Length;
                    if (stem.EndsWith("niss", StringComparison.Ordinal))
                    {
                        w.Length--;
                    }

                    break;
                case "s":
                    if (stem.Length > 0 && "bdfghklmnrt".Contains(stem[^1], StringComparison.Ordinal))
                    {
                        w.Length = stem.Length;
                    }

                    break;
            }
        }

        // Step 2
        s1 = w.ToString();
        suffix = Longest(s1, "est", "en", "er", "st");
        if (suffix is not null && s1.Length - suffix.Length >= r1)
        {
            var stem = s1[..^suffix.Length];
            if (suffix != "st" || (stem.Length >= 3 && "bdfghklmnt".Contains(stem[^1], StringComparison.Ordinal)))
            {
                w.Length = stem.Length;
            }
        }

        // Step 3
        s1 = w.ToString();
        suffix = Longest(s1, "keit", "lich", "heit", "isch", "end", "ung", "ig", "ik");
        if (suffix is not null && s1.Length - suffix.Length >= r2)
        {
            var stem = s1[..^suffix.Length];
            switch (suffix)
            {
                case "end" or "ung":
                    w.Length = stem.Length;
                    if (stem.EndsWith("ig", StringComparison.Ordinal) && stem.Length - 2 >= r2 && !stem.EndsWith("eig", StringComparison.Ordinal))
                    {
                        w.Length -= 2;
                    }

                    break;
                case "ig" or "ik" or "isch":
                    if (!stem.EndsWith('e'))
                    {
                        w.Length = stem.Length;
                    }

                    break;
                case "lich" or "heit":
                    w.Length = stem.Length;
                    if ((stem.EndsWith("er", StringComparison.Ordinal) || stem.EndsWith("en", StringComparison.Ordinal)) && stem.Length - 2 >= r1)
                    {
                        w.Length -= 2;
                    }

                    break;
                case "keit":
                    w.Length = stem.Length;
                    if (stem.EndsWith("lich", StringComparison.Ordinal) && stem.Length - 4 >= r2)
                    {
                        w.Length -= 4;
                    }
                    else if (stem.EndsWith("ig", StringComparison.Ordinal) && stem.Length - 2 >= r2)
                    {
                        w.Length -= 2;
                    }

                    break;
            }
        }

        return w.ToString().Replace('U', 'u').Replace('Y', 'y').Replace('ä', 'a').Replace('ö', 'o').Replace('ü', 'u');
    }
}
