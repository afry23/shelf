using System.Text;

namespace Shelf.Extraction;

/// <summary>
/// Reads a TXT file, the text of a notes file (F-NOTEF-3): UTF-8 with or without a byte order mark, else Latin-1. The text is cut
/// at blank lines and a place in the file is <c>line=&lt;n&gt;</c>, the first line of the passage.
/// </summary>
internal static class TextFileExtractor
{
    public static ExtractedBook Read(string path, bool withText)
    {
        var segments = new List<TextSegment>();
        if (withText)
        {
            var lines = Decode(File.ReadAllBytes(path)).Split('\n').Select(l => l.TrimEnd('\r')).ToList();
            var text = new StringBuilder();
            var start = 1;

            void Flush()
            {
                var body = text.ToString().Trim();
                if (body.Length > 0)
                {
                    segments.Add(new TextSegment("line=" + start.ToString(System.Globalization.CultureInfo.InvariantCulture), body));
                }

                text.Clear();
            }

            for (var i = 0; i < lines.Count; i++)
            {
                if (lines[i].Trim().Length == 0)
                {
                    Flush();
                    start = i + 2;
                    continue;
                }

                text.AppendLine(lines[i]);
            }

            Flush();
        }

        return new ExtractedBook(new BookMetadata(), null, segments);
    }

    /// <summary>UTF-8 (a byte order mark is dropped); bytes that are no UTF-8 are read as Latin-1.</summary>
    internal static string Decode(byte[] bytes)
    {
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        var content = bytes.AsSpan();
        if (content.StartsWith(Encoding.UTF8.Preamble))
        {
            content = content[Encoding.UTF8.Preamble.Length..];
        }

        try
        {
            return utf8.GetString(content);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(content);
        }
    }
}
