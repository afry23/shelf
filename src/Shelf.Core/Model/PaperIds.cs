using System.Text.RegularExpressions;

namespace Shelf.Core.Model;

/// <summary>
/// DOI and arXiv ID detection (F-PAP-2). A DOI is returned without prefix and trailing punctuation; an arXiv ID without
/// the version (<c>2301.12345v2</c> is <c>2301.12345</c>), so the versions of a paper are one work (F-PAP-9).
/// </summary>
public static partial class PaperIds
{
    // 10.<registrant of 4 to 9 digits>/<suffix>. SICI-style DOIs contain < and >, so those stay allowed. The suffix runs to the next white space; trailing punctuation is cut off
    // afterwards because it belongs to the sentence, not to the DOI.
    [GeneratedRegex(@"(?<![0-9A-Za-z])10\.[0-9]{4,9}/[^\s""]+", RegexOptions.CultureInvariant)]
    private static partial Regex DoiCandidate();

    // New style YYMM.NNNN or YYMM.NNNNN, old style archive[.XX]/YYMMNNN.
    [GeneratedRegex(@"(?<![0-9A-Za-z./-])(?:(?<new>(?:0[7-9]|[1-9][0-9])(?:0[1-9]|1[0-2])\.[0-9]{4,5})|(?<old>[a-z]+(?:-[a-z]+)?(?:\.[A-Z]{2})?/[0-9]{2}(?:0[1-9]|1[0-2])[0-9]{3}))(?:v[0-9]+)?(?![0-9A-Za-z])", RegexOptions.CultureInvariant)]
    private static partial Regex ArxivCandidate();

    /// <summary>The first DOI in <paramref name="text"/>, or null.</summary>
    public static string? FindDoi(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        foreach (Match match in DoiCandidate().Matches(text))
        {
            var doi = TrimTrailing(match.Value);
            if (doi.IndexOf('/') is var slash and > 0 && slash < doi.Length - 1)
            {
                return doi;
            }
        }

        return null;
    }

    /// <summary>The first arXiv ID in <paramref name="text"/> without its version, or null.</summary>
    public static string? FindArxivId(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        foreach (Match match in ArxivCandidate().Matches(text))
        {
            var id = match.Groups["new"].Success ? match.Groups["new"].Value : match.Groups["old"].Value;
            if (id.Length > 0)
            {
                return id;
            }
        }

        return null;
    }

    /// <summary>
    /// What a user typed to name a paper: a DOI (<c>10.1145/…</c>, <c>doi:…</c>, <c>https://doi.org/…</c>) or an arXiv ID
    /// (<c>1706.03762v5</c>, <c>arXiv:…</c>, <c>https://arxiv.org/abs/…</c>). The DOI of an arXiv paper
    /// (<c>10.48550/arXiv.1706.03762</c>) names the arXiv paper. Null if it is neither.
    /// </summary>
    public static PaperReference? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var trimmed = text.Trim();
        if (FindDoi(trimmed) is { } doi)
        {
            const string ArxivDoiPrefix = "10.48550/arxiv.";
            return doi.StartsWith(ArxivDoiPrefix, StringComparison.OrdinalIgnoreCase) && FindArxivId(doi[ArxivDoiPrefix.Length..]) is { } fromDoi
                ? new PaperReference(null, fromDoi)
                : new PaperReference(Normalize(doi), null);
        }

        // The ID in a link is preceded by a slash, which the pattern does not accept in running text.
        var link = ArxivLink().Match(trimmed);
        var candidate = link.Success ? link.Groups[1].Value : trimmed;
        return FindArxivId(candidate) is { } arxiv ? new PaperReference(null, arxiv) : null;
    }

    [GeneratedRegex(@"arxiv\.org/(?:abs|pdf)/(.+?)(?:\.pdf)?(?:[?#].*)?$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex ArxivLink();

    /// <summary>Lower-cases a DOI for comparison (DOIs are case-insensitive, F-PAP-9).</summary>
    public static string Normalize(string doi) => doi.Trim().ToLowerInvariant();

    // The characters a DOI may end with in running text are fewer than the ones a suffix may contain: cut sentence
    // punctuation, and brackets that are not closed inside the DOI.
    private static string TrimTrailing(string doi)
    {
        var end = doi.Length;
        while (end > 0)
        {
            var c = doi[end - 1];
            if (c is '.' or ',' or ';' or ':' or '\'' or '!' or '?')
            {
                end--;
            }
            else if (c is ')' or ']' or '}' && !Balanced(doi.AsSpan(0, end), c))
            {
                end--;
            }
            else
            {
                break;
            }
        }

        return doi[..end];
    }

    private static bool Balanced(ReadOnlySpan<char> text, char closing)
    {
        var opening = closing switch { ')' => '(', ']' => '[', _ => '{' };
        var open = 0;
        var close = 0;
        foreach (var c in text)
        {
            if (c == opening)
            {
                open++;
            }
            else if (c == closing)
            {
                close++;
            }
        }

        return open >= close;
    }
}

/// <summary>A paper named by its DOI (lower case) or its arXiv ID (without version); one of the two is set.</summary>
public sealed record PaperReference(string? Doi, string? ArxivId);
