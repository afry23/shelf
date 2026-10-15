using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Shelf.TestData.Formats;

/// <summary>A one-bit DeviceGray image drawn as the only content of a page (a "scanned page").</summary>
internal sealed record PdfImage(int Width, int Height, byte[] Data);

internal sealed class PdfSpec
{
    /// <summary>Text pages; each entry is the list of lines of one page.</summary>
    public IReadOnlyList<IReadOnlyList<string>> Pages { get; init; } = [];

    /// <summary>When set, the document has exactly one page that shows this image and no text operators.</summary>
    public PdfImage? Image { get; init; }

    /// <summary>When true an Info dictionary is written (with whichever of the values below are set).</summary>
    public bool HasInfo { get; init; }

    public string? Title { get; init; }

    public string? Author { get; init; }

    public string? Subject { get; init; }

    public string? Keywords { get; init; }

    public bool HasXmp { get; init; }

    public string IdentitySeed { get; init; } = "shelf";
}

/// <summary>Writes small, valid PDF 1.7 files: Helvetica text with WinAnsiEncoding, uncompressed streams, exact xref offsets.</summary>
internal static class PdfWriter
{
    public const int LinesPerPage = 45;
    public const string Producer = "shelf synthetic library generator";
    private const string FixedDate = "D:20000101000000Z";

    public static byte[] Write(PdfSpec spec)
    {
        var pageCount = spec.Image is not null ? 1 : Math.Max(1, spec.Pages.Count);
        var next = 4;
        var info = spec.HasInfo ? next++ : 0;
        var xmp = spec.HasXmp ? next++ : 0;
        var pageObjects = new int[pageCount];
        var contentObjects = new int[pageCount];
        for (var i = 0; i < pageCount; i++)
        {
            pageObjects[i] = next++;
            contentObjects[i] = next++;
        }

        var imageObject = spec.Image is not null ? next++ : 0;
        var size = next;

        using var output = new MemoryStream();
        var offsets = new long[size];

        Raw(output, "%PDF-1.7\n");
        output.Write([0x25, 0xE2, 0xE3, 0xCF, 0xD3, 0x0A]);

        Begin(output, offsets, 1);
        Raw(output, "<< /Type /Catalog /Pages 2 0 R");
        if (xmp != 0)
        {
            Raw(output, $" /Metadata {xmp} 0 R");
        }

        Raw(output, " >>\nendobj\n");

        Begin(output, offsets, 2);
        Raw(output, "<< /Type /Pages /Count " + pageCount.ToString(CultureInfo.InvariantCulture) + " /Kids [");
        Raw(output, string.Join(' ', pageObjects.Select(n => n.ToString(CultureInfo.InvariantCulture) + " 0 R")));
        Raw(output, "] >>\nendobj\n");

        Begin(output, offsets, 3);
        Raw(output, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>\nendobj\n");

        if (info != 0)
        {
            Begin(output, offsets, info);
            Raw(output, "<<");
            Entry(output, "Title", spec.Title);
            Entry(output, "Author", spec.Author);
            Entry(output, "Subject", spec.Subject);
            Entry(output, "Keywords", spec.Keywords);
            Raw(output, $" /Creator ({Producer}) /Producer ({Producer}) /CreationDate ({FixedDate}) /ModDate ({FixedDate}) >>\nendobj\n");
        }

        if (xmp != 0)
        {
            var packet = Xml.Bytes(XmpPacket(spec));
            Begin(output, offsets, xmp);
            Raw(output, $"<< /Type /Metadata /Subtype /XML /Length {packet.Length.ToString(CultureInfo.InvariantCulture)} >>\nstream\n");
            output.Write(packet);
            Raw(output, "\nendstream\nendobj\n");
        }

        for (var i = 0; i < pageCount; i++)
        {
            Begin(output, offsets, pageObjects[i]);
            Raw(output, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents " +
                contentObjects[i].ToString(CultureInfo.InvariantCulture) + " 0 R /Resources << ");
            if (spec.Image is not null)
            {
                Raw(output, "/XObject << /Im1 " + imageObject.ToString(CultureInfo.InvariantCulture) + " 0 R >> ");
            }
            else
            {
                Raw(output, "/Font << /F1 3 0 R >> ");
            }

            Raw(output, ">> >>\nendobj\n");

            var content = spec.Image is not null
                ? "q 468 0 0 648 72 72 cm /Im1 Do Q\n"
                : TextContent(spec.Pages.Count == 0 ? [] : spec.Pages[i]);
            var contentBytes = Latin1(content);
            Begin(output, offsets, contentObjects[i]);
            Raw(output, $"<< /Length {contentBytes.Length.ToString(CultureInfo.InvariantCulture)} >>\nstream\n");
            output.Write(contentBytes);
            Raw(output, "endstream\nendobj\n");
        }

        if (spec.Image is { } image)
        {
            Begin(output, offsets, imageObject);
            Raw(output, $"<< /Type /XObject /Subtype /Image /Width {image.Width.ToString(CultureInfo.InvariantCulture)} " +
                $"/Height {image.Height.ToString(CultureInfo.InvariantCulture)} /ColorSpace /DeviceGray /BitsPerComponent 1 " +
                $"/Length {image.Data.Length.ToString(CultureInfo.InvariantCulture)} >>\nstream\n");
            output.Write(image.Data);
            Raw(output, "\nendstream\nendobj\n");
        }

        var xref = output.Position;
        Raw(output, "xref\n0 " + size.ToString(CultureInfo.InvariantCulture) + "\n");
        Raw(output, "0000000000 65535 f \n");
        for (var n = 1; n < size; n++)
        {
            Raw(output, offsets[n].ToString("D10", CultureInfo.InvariantCulture) + " 00000 n \n");
        }

        var id = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(spec.IdentitySeed)).AsSpan(0, 16));
        Raw(output, "trailer\n<< /Size " + size.ToString(CultureInfo.InvariantCulture) + " /Root 1 0 R");
        if (info != 0)
        {
            Raw(output, $" /Info {info} 0 R");
        }

        Raw(output, $" /ID [<{id}> <{id}>] >>\nstartxref\n{xref.ToString(CultureInfo.InvariantCulture)}\n%%EOF\n");
        return output.ToArray();
    }

    /// <summary>Splits text into lines of at most <paramref name="width"/> characters at word boundaries.</summary>
    public static List<string> Wrap(string text, int width = 80)
    {
        var lines = new List<string>();
        var line = new StringBuilder();
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length > 0 && line.Length + 1 + word.Length > width)
            {
                lines.Add(line.ToString());
                line.Clear();
            }

            if (line.Length > 0)
            {
                line.Append(' ');
            }

            line.Append(word);
        }

        if (line.Length > 0)
        {
            lines.Add(line.ToString());
        }

        return lines;
    }

    /// <summary>Escapes a string for a PDF literal string: parentheses and backslash.</summary>
    internal static string EscapeLiteral(string text)
    {
        var b = new StringBuilder(text.Length + 4);
        foreach (var c in text)
        {
            if (c is '(' or ')' or '\\')
            {
                b.Append('\\');
            }

            b.Append(c);
        }

        return b.ToString();
    }

    /// <summary>Info strings: ASCII as a literal string, anything else as UTF-16BE with a byte order mark.</summary>
    internal static string TextString(string value)
    {
        if (value.All(c => c is >= ' ' and <= '~'))
        {
            return "(" + EscapeLiteral(value) + ")";
        }

        var bytes = Encoding.BigEndianUnicode.GetBytes(value);
        return "<FEFF" + Convert.ToHexString(bytes) + ">";
    }

    private static string TextContent(IReadOnlyList<string> lines)
    {
        var b = new StringBuilder();
        b.Append("BT\n/F1 11 Tf\n14 TL\n72 720 Td\n");
        foreach (var line in lines)
        {
            b.Append('(').Append(EscapeLiteral(line)).Append(") Tj\nT*\n");
        }

        b.Append("ET\n");
        return b.ToString();
    }

    private static string XmpPacket(PdfSpec spec)
    {
        var b = new StringBuilder();
        b.Append("<?xpacket begin=\"﻿\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?>\n");
        b.Append("<x:xmpmeta xmlns:x=\"adobe:ns:meta/\">\n<rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\">\n");
        b.Append("<rdf:Description rdf:about=\"\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:pdf=\"http://ns.adobe.com/pdf/1.3/\">\n");
        if (spec.Title is not null)
        {
            b.Append("<dc:title><rdf:Alt><rdf:li xml:lang=\"x-default\">").Append(Xml.Escape(spec.Title)).Append("</rdf:li></rdf:Alt></dc:title>\n");
        }

        if (spec.Author is not null)
        {
            b.Append("<dc:creator><rdf:Seq><rdf:li>").Append(Xml.Escape(spec.Author)).Append("</rdf:li></rdf:Seq></dc:creator>\n");
        }

        b.Append("<pdf:Producer>").Append(Producer).Append("</pdf:Producer>\n");
        b.Append("</rdf:Description>\n</rdf:RDF>\n</x:xmpmeta>\n<?xpacket end=\"w\"?>");
        return b.ToString();
    }

    private static void Entry(Stream output, string key, string? value)
    {
        if (value is not null)
        {
            Raw(output, " /" + key + " ");
            output.Write(Latin1(TextString(value)));
        }
    }

    private static void Begin(Stream output, long[] offsets, int number)
    {
        offsets[number] = output.Position;
        Raw(output, number.ToString(CultureInfo.InvariantCulture) + " 0 obj\n");
    }

    private static void Raw(Stream output, string ascii) => output.Write(Latin1(ascii));

    /// <summary>Latin-1 bytes; equal to WinAnsiEncoding for the characters the generator uses (0x20-0x7E, 0xA0-0xFF).</summary>
    private static byte[] Latin1(string text)
    {
        var bytes = new byte[text.Length];
        for (var i = 0; i < text.Length; i++)
        {
            bytes[i] = text[i] <= 0xFF ? (byte)text[i] : (byte)'?';
        }

        return bytes;
    }
}
