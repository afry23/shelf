using System.Text;
using System.Text.RegularExpressions;

namespace Shelf.Extraction;

/// <summary>
/// Reads a Markdown file, the text of an article (F-ART-7): the title is the first top-level heading, the text is cut at the
/// headings, and a place in the file is <c>heading=&lt;slug&gt;</c> (<c>heading=start</c> before the first heading). Links and
/// images count as their text so that an address is not searched like a word.
/// </summary>
internal static partial class MarkdownExtractor
{
    public static ExtractedBook Read(string path, bool withText)
    {
        var markdown = File.ReadAllText(path, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false));
        string? title = null;
        var segments = new List<TextSegment>();
        var slugs = new Dictionary<string, int>(StringComparer.Ordinal);
        var locator = "heading=start";
        var text = new StringBuilder();
        var inFence = false;

        void Flush()
        {
            var body = text.ToString().Trim();
            if (withText && body.Length > 0)
            {
                segments.Add(new TextSegment(locator, body));
            }

            text.Clear();
        }

        foreach (var line in markdown.Split('\n').Select(l => l.TrimEnd('\r')))
        {
            if (line.StartsWith("```", StringComparison.Ordinal) || line.StartsWith("~~~", StringComparison.Ordinal))
            {
                inFence = !inFence;
                continue;
            }

            if (!inFence && Heading().Match(line) is { Success: true } heading)
            {
                var name = Plain(heading.Groups["text"].Value);
                if (heading.Groups["level"].Length == 1 && title is null && name.Length > 0)
                {
                    title = name;
                }

                Flush();
                locator = "heading=" + Slug(name, slugs);
                text.AppendLine(name);
                continue;
            }

            text.AppendLine(Plain(line));
        }

        Flush();
        return new ExtractedBook(new BookMetadata { Title = title, TitleOrigin = title is null ? MetadataOrigin.None : MetadataOrigin.FileContent }, null, segments);
    }

    /// <summary>The slug of a heading: lower case letters and digits joined by hyphens; later headings with the same slug get <c>-2</c>, <c>-3</c>.</summary>
    internal static string Slug(string heading, Dictionary<string, int> used)
    {
        var slug = new StringBuilder();
        foreach (var c in heading.Normalize(NormalizationForm.FormKC))
        {
            if (char.IsLetterOrDigit(c))
            {
                slug.Append(char.ToLowerInvariant(c));
            }
            else if (slug.Length > 0 && slug[^1] != '-')
            {
                slug.Append('-');
            }
        }

        var key = slug.ToString().Trim('-');
        if (key.Length == 0)
        {
            key = "section";
        }

        used[key] = used.GetValueOrDefault(key) + 1;
        return used[key] == 1 ? key : key + "-" + used[key].ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string Plain(string line)
    {
        var text = Images().Replace(line, "${alt}");
        text = Links().Replace(text, "${text}");
        return Marks().Replace(text, string.Empty).Trim();
    }

    [GeneratedRegex(@"^(?<level>#{1,6})\s+(?<text>.+?)\s*#*\s*$")]
    private static partial Regex Heading();

    [GeneratedRegex(@"!\[(?<alt>[^\]]*)\]\([^)]*\)")]
    private static partial Regex Images();

    [GeneratedRegex(@"\[(?<text>[^\]]*)\]\([^)]*\)")]
    private static partial Regex Links();

    [GeneratedRegex(@"(\*\*|__|`|^>\s?|^\s*[-*+]\s+)", RegexOptions.Multiline)]
    private static partial Regex Marks();
}
