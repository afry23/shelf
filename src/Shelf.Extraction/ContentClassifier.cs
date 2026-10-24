using System.Text.RegularExpressions;
using Shelf.Core.Model;

namespace Shelf.Extraction;

/// <summary>What a piece of text is. Everything but <see cref="Body"/> is left out of the embeddings (F-SEM-9) but stays in the full-text index.</summary>
public enum ContentKind
{
    Body,
    Toc,
    FrontMatter,
    Index,
    BackMatter,
    Code,
}

/// <summary>
/// Tells prose from table of contents, front and back matter, index and source code. By structure where a file names it
/// (EPUB <c>epub:type</c>, a well-known section title, a PDF outline) and by the look of the text where it does not.
/// Deliberately cautious: a doubtful piece of text counts as <see cref="ContentKind.Body"/>.
/// </summary>
public static partial class ContentClassifier
{
    private const int MinWords = 20;

    // Share of non-white-space characters that are typical for code, above which text counts as code. Prose stays below 0.02.
    private const double CodeSymbolShare = 0.08;

    // Share of words that are numbers or page ranges, above which a text with hardly any sentences counts as an index.
    private const double IndexNumberShare = 0.25;

    private static readonly Dictionary<string, ContentKind> EpubTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["toc"] = ContentKind.Toc,
        ["landmarks"] = ContentKind.Toc,
        ["loi"] = ContentKind.Toc,
        ["lot"] = ContentKind.Toc,
        ["index"] = ContentKind.Index,
        ["frontmatter"] = ContentKind.FrontMatter,
        ["cover"] = ContentKind.FrontMatter,
        ["titlepage"] = ContentKind.FrontMatter,
        ["halftitlepage"] = ContentKind.FrontMatter,
        ["copyright-page"] = ContentKind.FrontMatter,
        ["dedication"] = ContentKind.FrontMatter,
        ["epigraph"] = ContentKind.FrontMatter,
        ["foreword"] = ContentKind.FrontMatter,
        ["preface"] = ContentKind.FrontMatter,
        ["acknowledgments"] = ContentKind.FrontMatter,
        ["acknowledgements"] = ContentKind.FrontMatter,
        ["bibliography"] = ContentKind.BackMatter,
        ["glossary"] = ContentKind.BackMatter,
        ["colophon"] = ContentKind.BackMatter,
        ["backmatter"] = ContentKind.BackMatter,
        ["rearnotes"] = ContentKind.BackMatter,
        ["endnotes"] = ContentKind.BackMatter,
    };

    private static readonly Dictionary<string, ContentKind> Titles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["contents"] = ContentKind.Toc,
        ["table of contents"] = ContentKind.Toc,
        ["inhalt"] = ContentKind.Toc,
        ["inhaltsverzeichnis"] = ContentKind.Toc,
        ["inhaltsübersicht"] = ContentKind.Toc,
        ["sommaire"] = ContentKind.Toc,
        ["table des matières"] = ContentKind.Toc,
        ["índice"] = ContentKind.Toc,
        ["index"] = ContentKind.Index,
        ["stichwortverzeichnis"] = ContentKind.Index,
        ["sachregister"] = ContentKind.Index,
        ["register"] = ContentKind.Index,
        ["preface"] = ContentKind.FrontMatter,
        ["foreword"] = ContentKind.FrontMatter,
        ["introduction to the second edition"] = ContentKind.FrontMatter,
        ["acknowledgments"] = ContentKind.FrontMatter,
        ["acknowledgements"] = ContentKind.FrontMatter,
        ["about the author"] = ContentKind.FrontMatter,
        ["about the authors"] = ContentKind.FrontMatter,
        ["copyright"] = ContentKind.FrontMatter,
        ["dedication"] = ContentKind.FrontMatter,
        ["title page"] = ContentKind.FrontMatter,
        ["vorwort"] = ContentKind.FrontMatter,
        ["geleitwort"] = ContentKind.FrontMatter,
        ["danksagung"] = ContentKind.FrontMatter,
        ["über den autor"] = ContentKind.FrontMatter,
        ["über die autoren"] = ContentKind.FrontMatter,
        ["impressum"] = ContentKind.FrontMatter,
        ["bibliography"] = ContentKind.BackMatter,
        ["references"] = ContentKind.BackMatter,
        ["glossary"] = ContentKind.BackMatter,
        ["colophon"] = ContentKind.BackMatter,
        ["literaturverzeichnis"] = ContentKind.BackMatter,
        ["literatur"] = ContentKind.BackMatter,
        ["quellenverzeichnis"] = ContentKind.BackMatter,
        ["glossar"] = ContentKind.BackMatter,
    };

    /// <summary>The kind for the value of an <c>epub:type</c> attribute (a space-separated list); the first known one wins.</summary>
    public static ContentKind FromEpubType(string? value)
    {
        if (value is null)
        {
            return ContentKind.Body;
        }

        foreach (var token in value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            if (EpubTypes.TryGetValue(token, out var kind))
            {
                return kind;
            }
        }

        return ContentKind.Body;
    }

    /// <summary>The kind for a section title, if it is exactly one of the well-known ones (leading numbering and trailing colons ignored).</summary>
    public static ContentKind FromTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return ContentKind.Body;
        }

        var normalized = Numbering().Replace(title, string.Empty).Trim().TrimEnd(':', '.').Trim();
        return Titles.TryGetValue(normalized, out var kind) ? kind : ContentKind.Body;
    }

    /// <summary>
    /// The kind of one chunk: what its segment already is, or, for plain text, what its look says. Chunks of fewer than
    /// 20 words are judged by their look only for a table of contents with dot leaders.
    /// </summary>
    public static ContentKind Classify(ContentKind segmentKind, string text)
    {
        if (segmentKind != ContentKind.Body)
        {
            return segmentKind;
        }

        if (DotLeader().Matches(text).Count >= 4)
        {
            return ContentKind.Toc;
        }

        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < MinWords)
        {
            return ContentKind.Body;
        }

        if (LooksLikeIndex(words))
        {
            return ContentKind.Index;
        }

        return LooksLikeCode(text) ? ContentKind.Code : ContentKind.Body;
    }

    /// <summary>
    /// The kind of each PDF page from an outline: the pages of an entry with a well-known title, up to the page of the next
    /// entry of the same or a higher level. Pages not covered are <see cref="ContentKind.Body"/>.
    /// </summary>
    public static IReadOnlyDictionary<int, ContentKind> PdfPageKinds(IReadOnlyList<TocEntry> outline, int pageCount)
    {
        var kinds = new Dictionary<int, ContentKind>();
        for (var i = 0; i < outline.Count; i++)
        {
            var kind = FromTitle(outline[i].Title);
            if (kind == ContentKind.Body || !Locators.TryParsePdfPage(outline[i].Locator, out var first))
            {
                continue;
            }

            var last = pageCount;
            for (var j = i + 1; j < outline.Count; j++)
            {
                if (outline[j].Level <= outline[i].Level && Locators.TryParsePdfPage(outline[j].Locator, out var next))
                {
                    last = next - 1;
                    break;
                }
            }

            for (var page = first; page <= last; page++)
            {
                kinds.TryAdd(page, kind);
            }
        }

        return kinds;
    }

    private static bool LooksLikeIndex(string[] words)
    {
        var numbers = words.Count(w => NumberOrRange().IsMatch(w));
        var sentenceEnds = words.Count(w => w.Length > 3 && w[^1] == '.' && char.IsLetter(w[^2]));
        return (double)numbers / words.Length >= IndexNumberShare && sentenceEnds * 100 < words.Length;
    }

    private static bool LooksLikeCode(string text)
    {
        var symbols = 0;
        var total = 0;
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                continue;
            }

            total++;
            if (c is '{' or '}' or '(' or ')' or ';' or '=' or '<' or '>' or '[' or ']' or '_')
            {
                symbols++;
            }
        }

        return total > 0 && (double)symbols / total >= CodeSymbolShare;
    }

    [GeneratedRegex(@"^\s*(\d+(\.\d+)*|[IVXLC]+)[.)]?\s+")]
    private static partial Regex Numbering();

    // "......" or ". . . ." followed by a page number
    [GeneratedRegex(@"(\.\s?){4,}\s*\d+")]
    private static partial Regex DotLeader();

    // 12, 45-47, 45–47 (with a trailing comma)
    [GeneratedRegex(@"^\d+([-–]\d+)?,?$")]
    private static partial Regex NumberOrRange();
}
