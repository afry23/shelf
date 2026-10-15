using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Shelf.TestData.Content;

namespace Shelf.TestData.Formats;

internal sealed record EpubAuthor(string Name, string FileAs);

internal sealed class EpubSpec
{
    /// <summary>2 (EPUB 2.0.1) or 3 (EPUB 3.0).</summary>
    public int Version { get; init; } = 3;

    /// <summary>Null omits the element, an empty string writes an empty element.</summary>
    public string? Title { get; init; }

    public IReadOnlyList<EpubAuthor> Authors { get; init; } = [];

    public string? Language { get; init; }

    /// <summary>Used as the book identifier when set; otherwise a UUID derived from <see cref="IdentitySeed"/>.</summary>
    public string? Isbn13 { get; init; }

    /// <summary>Stable text from which the UUID identifier is derived (no random GUIDs).</summary>
    public string IdentitySeed { get; init; } = "shelf";

    public string? SeriesName { get; init; }

    public int SeriesIndex { get; init; }

    public byte[]? CoverPng { get; init; }

    public required TextDocument Document { get; init; }

    /// <summary>Writes an OPF whose metadata element is never closed (not well-formed XML).</summary>
    public bool MalformedOpf { get; init; }

    /// <summary>Deflate for entries except mimetype. Output is then not guaranteed byte-identical across platforms.</summary>
    public bool Deflate { get; init; }
}

/// <summary>Writes valid minimal EPUB 2.0.1 / 3.0 containers. All text is built with "\n" newlines and no BOM.</summary>
internal static class EpubWriter
{
    private static readonly DateTimeOffset FixedTime = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static byte[] Write(EpubSpec spec)
    {
        var chapterNames = spec.Document.Chapters.Select((_, i) => $"chapter-{i + 1:D2}.xhtml").ToList();
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            // The mimetype entry must be first, stored and without an extra field.
            AddEntry(archive, "mimetype", Xml.Bytes("application/epub+zip"), CompressionLevel.NoCompression);
            var level = spec.Deflate ? CompressionLevel.Optimal : CompressionLevel.NoCompression;
            AddEntry(archive, "META-INF/container.xml", Xml.Bytes(Container()), level);
            AddEntry(archive, "OEBPS/content.opf", Xml.Bytes(Opf(spec, chapterNames)), level);
            AddEntry(archive, spec.Version == 3 ? "OEBPS/nav.xhtml" : "OEBPS/toc.ncx", Xml.Bytes(spec.Version == 3 ? Nav(spec, chapterNames) : Ncx(spec, chapterNames)), level);
            for (var i = 0; i < chapterNames.Count; i++)
            {
                AddEntry(archive, "OEBPS/" + chapterNames[i], Xml.Bytes(Chapter(spec, spec.Document.Chapters[i])), level);
            }

            if (spec.CoverPng is not null)
            {
                AddEntry(archive, "OEBPS/images/cover.png", spec.CoverPng, level);
            }
        }

        return buffer.ToArray();
    }

    internal static string UuidFor(string seed)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        hash[6] = (byte)((hash[6] & 0x0F) | 0x40);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        var hex = Convert.ToHexStringLower(hash.AsSpan(0, 16));
        return $"{hex[..8]}-{hex[8..12]}-{hex[12..16]}-{hex[16..20]}-{hex[20..32]}";
    }

    private static void AddEntry(ZipArchive archive, string name, byte[] content, CompressionLevel level)
    {
        var entry = archive.CreateEntry(name, level);
        entry.LastWriteTime = FixedTime;
        using var stream = entry.Open();
        stream.Write(content);
    }

    private static string Container() =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
        "<container version=\"1.0\" xmlns=\"urn:oasis:names:tc:opendocument:xmlns:container\">\n" +
        "  <rootfiles>\n" +
        "    <rootfile full-path=\"OEBPS/content.opf\" media-type=\"application/oebps-package+xml\"/>\n" +
        "  </rootfiles>\n" +
        "</container>\n";

    private static string Identifier(EpubSpec spec) =>
        spec.Isbn13 is not null ? "urn:isbn:" + spec.Isbn13 : "urn:uuid:" + UuidFor(spec.IdentitySeed);

    private static string Opf(EpubSpec spec, List<string> chapterNames)
    {
        var b = new StringBuilder();
        b.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
        var epub3 = spec.Version == 3;
        b.Append("<package xmlns=\"http://www.idpf.org/2007/opf\" unique-identifier=\"bookid\" version=\"")
            .Append(epub3 ? "3.0" : "2.0").Append('"');
        if (epub3 && spec.Language is not null)
        {
            b.Append(" xml:lang=\"").Append(Xml.Escape(spec.Language)).Append('"');
        }

        b.Append(">\n  <metadata xmlns:dc=\"http://purl.org/dc/elements/1.1/\"");
        b.Append(epub3 ? "" : " xmlns:opf=\"http://www.idpf.org/2007/opf\"").Append(">\n");

        if (epub3)
        {
            b.Append("    <dc:identifier id=\"bookid\">").Append(Xml.Escape(Identifier(spec))).Append("</dc:identifier>\n");
        }
        else
        {
            var scheme = spec.Isbn13 is not null ? "ISBN" : "UUID";
            var value = spec.Isbn13 ?? "urn:uuid:" + UuidFor(spec.IdentitySeed);
            b.Append("    <dc:identifier id=\"bookid\" opf:scheme=\"").Append(scheme).Append("\">")
                .Append(Xml.Escape(value)).Append("</dc:identifier>\n");
        }

        if (spec.Title is not null)
        {
            b.Append("    <dc:title>").Append(Xml.Escape(spec.Title)).Append("</dc:title>\n");
        }

        for (var i = 0; i < spec.Authors.Count; i++)
        {
            var author = spec.Authors[i];
            if (epub3)
            {
                var id = "creator" + (i + 1);
                b.Append("    <dc:creator id=\"").Append(id).Append("\">").Append(Xml.Escape(author.Name)).Append("</dc:creator>\n");
                b.Append("    <meta refines=\"#").Append(id).Append("\" property=\"file-as\">").Append(Xml.Escape(author.FileAs)).Append("</meta>\n");
                b.Append("    <meta refines=\"#").Append(id).Append("\" property=\"role\" scheme=\"marc:relators\">aut</meta>\n");
            }
            else
            {
                b.Append("    <dc:creator opf:role=\"aut\" opf:file-as=\"").Append(Xml.Escape(author.FileAs)).Append("\">")
                    .Append(Xml.Escape(author.Name)).Append("</dc:creator>\n");
            }
        }

        if (spec.Language is not null)
        {
            b.Append("    <dc:language>").Append(Xml.Escape(spec.Language)).Append("</dc:language>\n");
        }

        if (epub3)
        {
            b.Append("    <meta property=\"dcterms:modified\">2000-01-01T00:00:00Z</meta>\n");
            if (spec.SeriesName is not null)
            {
                b.Append("    <meta property=\"belongs-to-collection\" id=\"series1\">").Append(Xml.Escape(spec.SeriesName)).Append("</meta>\n");
                b.Append("    <meta refines=\"#series1\" property=\"collection-type\">series</meta>\n");
                b.Append("    <meta refines=\"#series1\" property=\"group-position\">").Append(spec.SeriesIndex).Append("</meta>\n");
            }
        }
        else
        {
            if (spec.SeriesName is not null)
            {
                b.Append("    <meta name=\"calibre:series\" content=\"").Append(Xml.Escape(spec.SeriesName)).Append("\"/>\n");
                b.Append("    <meta name=\"calibre:series_index\" content=\"").Append(spec.SeriesIndex).Append("\"/>\n");
            }

            if (spec.CoverPng is not null)
            {
                b.Append("    <meta name=\"cover\" content=\"cover-image\"/>\n");
            }
        }

        if (!spec.MalformedOpf)
        {
            b.Append("  </metadata>\n");
        }

        b.Append("  <manifest>\n");
        if (epub3)
        {
            b.Append("    <item id=\"nav\" href=\"nav.xhtml\" media-type=\"application/xhtml+xml\" properties=\"nav\"/>\n");
        }
        else
        {
            b.Append("    <item id=\"ncx\" href=\"toc.ncx\" media-type=\"application/x-dtbncx+xml\"/>\n");
        }

        for (var i = 0; i < chapterNames.Count; i++)
        {
            b.Append("    <item id=\"chapter").Append(i + 1).Append("\" href=\"").Append(chapterNames[i])
                .Append("\" media-type=\"application/xhtml+xml\"/>\n");
        }

        if (spec.CoverPng is not null)
        {
            b.Append("    <item id=\"cover-image\" href=\"images/cover.png\" media-type=\"image/png\"")
                .Append(epub3 ? " properties=\"cover-image\"" : string.Empty).Append("/>\n");
        }

        b.Append("  </manifest>\n");
        b.Append(epub3 ? "  <spine>\n" : "  <spine toc=\"ncx\">\n");
        for (var i = 0; i < chapterNames.Count; i++)
        {
            b.Append("    <itemref idref=\"chapter").Append(i + 1).Append("\"/>\n");
        }

        b.Append("  </spine>\n</package>\n");
        return b.ToString();
    }

    private static string Nav(EpubSpec spec, List<string> chapterNames)
    {
        var b = new StringBuilder();
        b.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<!DOCTYPE html>\n");
        b.Append("<html xmlns=\"http://www.w3.org/1999/xhtml\" xmlns:epub=\"http://www.idpf.org/2007/ops\">\n");
        b.Append("<head>\n<meta charset=\"utf-8\"/>\n<title>Contents</title>\n</head>\n<body>\n");
        b.Append("<nav epub:type=\"toc\" id=\"toc\">\n<h1>Contents</h1>\n<ol>\n");
        for (var i = 0; i < chapterNames.Count; i++)
        {
            b.Append("<li><a href=\"").Append(chapterNames[i]).Append("\">")
                .Append(Xml.Escape(spec.Document.Chapters[i].Title)).Append("</a></li>\n");
        }

        b.Append("</ol>\n</nav>\n</body>\n</html>\n");
        return b.ToString();
    }

    private static string Ncx(EpubSpec spec, List<string> chapterNames)
    {
        var b = new StringBuilder();
        b.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
        b.Append("<ncx xmlns=\"http://www.daisy.org/z3986/2005/ncx/\" version=\"2005-1\">\n<head>\n");
        b.Append("<meta name=\"dtb:uid\" content=\"").Append(Xml.Escape(spec.Isbn13 ?? "urn:uuid:" + UuidFor(spec.IdentitySeed))).Append("\"/>\n");
        b.Append("<meta name=\"dtb:depth\" content=\"1\"/>\n<meta name=\"dtb:totalPageCount\" content=\"0\"/>\n<meta name=\"dtb:maxPageNumber\" content=\"0\"/>\n</head>\n");
        b.Append("<docTitle><text>").Append(Xml.Escape(spec.Title ?? string.Empty)).Append("</text></docTitle>\n<navMap>\n");
        for (var i = 0; i < chapterNames.Count; i++)
        {
            b.Append("<navPoint id=\"navpoint-").Append(i + 1).Append("\" playOrder=\"").Append(i + 1).Append("\">\n");
            b.Append("<navLabel><text>").Append(Xml.Escape(spec.Document.Chapters[i].Title)).Append("</text></navLabel>\n");
            b.Append("<content src=\"").Append(chapterNames[i]).Append("\"/>\n</navPoint>\n");
        }

        b.Append("</navMap>\n</ncx>\n");
        return b.ToString();
    }

    private static string Chapter(EpubSpec spec, Content.Chapter chapter)
    {
        var b = new StringBuilder();
        b.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
        var language = spec.Language is null ? string.Empty : $" lang=\"{Xml.Escape(spec.Language)}\" xml:lang=\"{Xml.Escape(spec.Language)}\"";
        if (spec.Version == 3)
        {
            b.Append("<!DOCTYPE html>\n<html xmlns=\"http://www.w3.org/1999/xhtml\"").Append(language).Append(">\n");
        }
        else
        {
            b.Append("<!DOCTYPE html PUBLIC \"-//W3C//DTD XHTML 1.1//EN\" \"http://www.w3.org/TR/xhtml11/DTD/xhtml11.dtd\">\n");
            b.Append("<html xmlns=\"http://www.w3.org/1999/xhtml\"").Append(language).Append(">\n");
        }

        b.Append("<head>\n<meta charset=\"utf-8\"/>\n<title>").Append(Xml.Escape(chapter.Title)).Append("</title>\n</head>\n<body>\n");
        b.Append("<h1>").Append(Xml.Escape(chapter.Title)).Append("</h1>\n");
        foreach (var paragraph in chapter.Paragraphs)
        {
            b.Append("<p>").Append(Xml.Escape(paragraph)).Append("</p>\n");
        }

        b.Append("</body>\n</html>\n");
        return b.ToString();
    }
}
