using System.Net;
using System.Text;
using System.Xml;

namespace Proto;

/// <summary>Two AOT-safe XHTML-to-text variants (plan 3.4). (a) strict XmlReader; (b) tolerant tag stripper (used by the gates).</summary>
public static class XhtmlText
{
    private static readonly HashSet<string> Block = new(StringComparer.Ordinal)
    {
        "p", "div", "br", "h1", "h2", "h3", "h4", "h5", "h6", "li", "ul", "ol", "tr", "table", "section", "article",
        "blockquote", "pre", "hr", "dt", "dd", "header", "footer", "nav", "aside", "figure", "figcaption", "body", "title",
    };

    private static readonly HashSet<string> Skipped = new(StringComparer.Ordinal) { "head", "script", "style" };

    /// <summary>Variant (a): XmlReader with DtdProcessing.Ignore and no resolver; honors the encoding declaration of a byte stream.</summary>
    public static string Strict(Stream xhtml)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };
        using var reader = XmlReader.Create(xhtml, settings);
        var sb = new StringBuilder();
        reader.Read();
        while (!reader.EOF)
        {
            switch (reader.NodeType)
            {
                case XmlNodeType.Element:
                    var name = reader.LocalName.ToLowerInvariant();
                    if (Skipped.Contains(name))
                    {
                        reader.Skip();
                        continue;
                    }

                    if (Block.Contains(name))
                    {
                        sb.Append('\n');
                    }

                    break;
                case XmlNodeType.EndElement:
                    if (Block.Contains(reader.LocalName.ToLowerInvariant()))
                    {
                        sb.Append('\n');
                    }

                    break;
                case XmlNodeType.Text or XmlNodeType.CDATA or XmlNodeType.Whitespace or XmlNodeType.SignificantWhitespace:
                    sb.Append(reader.Value);
                    break;
            }

            reader.Read();
        }

        return sb.ToString();
    }

    /// <summary>Variant (b): tolerant state machine; tags are dropped, block tags become line breaks, entities decoded with WebUtility.HtmlDecode.</summary>
    public static string Tolerant(string html)
    {
        var sb = new StringBuilder(html.Length);
        var i = 0;
        var n = html.Length;
        while (i < n)
        {
            var c = html[i];
            if (c != '<' || i + 1 >= n || !(char.IsAsciiLetter(html[i + 1]) || html[i + 1] is '/' or '!' or '?'))
            {
                sb.Append(c);
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
                sb.Append(html, i + 9, stop - (i + 9));
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

            var name = html.AsSpan(nameStart, p - nameStart);
            var lower = name.ToString().ToLowerInvariant();
            char quote = '\0';
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

            var selfClosing = p > 0 && p <= n && p - 1 >= 0 && p - 1 < n && html[p - 1] == '/';
            i = p < n ? p + 1 : n;
            var local = lower.Contains(':') ? lower[(lower.IndexOf(':') + 1)..] : lower;
            if (!closing && !selfClosing && Skipped.Contains(local))
            {
                var closeIdx = html.IndexOf("</" + lower, i, StringComparison.OrdinalIgnoreCase);
                if (closeIdx < 0)
                {
                    i = n;
                }
                else
                {
                    var end = html.IndexOf('>', closeIdx);
                    i = end < 0 ? n : end + 1;
                }

                continue;
            }

            if (Block.Contains(local))
            {
                sb.Append('\n');
            }
        }

        return WebUtility.HtmlDecode(sb.ToString());
    }
}
