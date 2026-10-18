using System.Net;
using System.Text;

namespace Shelf.Extraction;

/// <summary>
/// Plain text from an XHTML chapter. A tolerant scanner, not an XML parser: real chapters contain named
/// entities (<c>&amp;nbsp;</c>) and other defects that make a strict <c>XmlReader</c> fail (ADR 0005, R-E10).
/// </summary>
internal static class XhtmlText
{
    private static readonly HashSet<string> Block = new(StringComparer.Ordinal)
    {
        "p", "div", "br", "h1", "h2", "h3", "h4", "h5", "h6", "li", "ul", "ol", "tr", "table", "section", "article",
        "blockquote", "pre", "hr", "dt", "dd", "header", "footer", "nav", "aside", "figure", "figcaption", "body", "title",
    };

    private static readonly HashSet<string> Skipped = new(StringComparer.Ordinal) { "head", "script", "style" };

    public static string ToPlainText(string html)
    {
        var text = new StringBuilder(html.Length);
        var i = 0;
        var n = html.Length;
        while (i < n)
        {
            var c = html[i];
            if (c != '<' || i + 1 >= n || !(char.IsAsciiLetter(html[i + 1]) || html[i + 1] is '/' or '!' or '?'))
            {
                text.Append(c);
                i++;
                continue;
            }

            if (string.CompareOrdinal(html, i, "<!--", 0, 4) == 0)
            {
                var end = html.IndexOf("-->", i + 4, StringComparison.Ordinal);
                i = end < 0 ? n : end + 3;
                continue;
            }

            if (string.CompareOrdinal(html, i, "<![CDATA[", 0, 9) == 0)
            {
                var end = html.IndexOf("]]>", i + 9, StringComparison.Ordinal);
                var stop = end < 0 ? n : end;
                text.Append(html, i + 9, stop - (i + 9));
                i = end < 0 ? n : end + 3;
                continue;
            }

            if (html[i + 1] is '!' or '?')
            {
                var end = html.IndexOf('>', i);
                i = end < 0 ? n : end + 1;
                continue;
            }

            var p = i + 1;
            var closing = html[p] == '/';
            if (closing)
            {
                p++;
            }

            var nameStart = p;
            while (p < n && (char.IsAsciiLetterOrDigit(html[p]) || html[p] is ':' or '-' or '_'))
            {
                p++;
            }

            var lower = html.AsSpan(nameStart, p - nameStart).ToString().ToLowerInvariant();

            // To the end of the tag; a '>' inside a quoted attribute value does not end it.
            var quote = '\0';
            while (p < n)
            {
                var d = html[p];
                if (quote != '\0')
                {
                    if (d == quote)
                    {
                        quote = '\0';
                    }
                }
                else if (d is '"' or '\'')
                {
                    quote = d;
                }
                else if (d == '>')
                {
                    break;
                }

                p++;
            }

            var selfClosing = p > 0 && p - 1 < n && html[p - 1] == '/';
            i = p < n ? p + 1 : n;
            var colon = lower.IndexOf(':', StringComparison.Ordinal);
            var local = colon >= 0 ? lower[(colon + 1)..] : lower;
            if (!closing && !selfClosing && Skipped.Contains(local))
            {
                var closeIndex = html.IndexOf("</" + lower, i, StringComparison.OrdinalIgnoreCase);
                if (closeIndex < 0)
                {
                    i = n;
                }
                else
                {
                    var end = html.IndexOf('>', closeIndex);
                    i = end < 0 ? n : end + 1;
                }

                continue;
            }

            if (Block.Contains(local))
            {
                text.Append('\n');
            }
        }

        return WebUtility.HtmlDecode(text.ToString());
    }
}
