using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Shelf.TestData.Content;
using Shelf.TestData.Formats;
using UglyToad.PdfPig;
using VersOne.Epub;
using Xunit;

namespace Shelf.TestData.Tests;

public sealed class FormatTests
{
    private static TextDocument Document(string language = "en") => TextGenerator.Generate(language, 21, 600);

    private static EpubSpec Spec(int version, bool cover = false, string? isbn = "9780000000019", string title = "Fish & Chips <1>") => new()
    {
        Version = version,
        Title = title,
        Authors = [new EpubAuthor("Martin Fowler", "Fowler, Martin"), new EpubAuthor("Ana Núñez", "Núñez, Ana")],
        Language = "en",
        Isbn13 = isbn,
        IdentitySeed = "unit",
        SeriesName = "Unit Series",
        SeriesIndex = 3,
        CoverPng = cover ? PngWriter.Write(64, 96, 10, 120, 200) : null,
        Document = Document(),
    };

    [Fact]
    public void ZipStartsWithAStoredMimetypeEntryWithoutExtraField()
    {
        var bytes = EpubWriter.Write(Spec(3));
        Assert.True(bytes.AsSpan().StartsWith(new byte[] { 0x50, 0x4B, 0x03, 0x04 }));
        var flags = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(6, 2));
        var method = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(8, 2));
        var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(26, 2));
        var extraLength = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(28, 2));
        Assert.Equal(0, flags & 0x08);
        Assert.Equal(0, method);
        Assert.Equal("mimetype", Encoding.ASCII.GetString(bytes, 30, nameLength));
        Assert.Equal(0, extraLength);
        Assert.Equal(38, 30 + nameLength + extraLength);
        Assert.Equal("application/epub+zip", Encoding.ASCII.GetString(bytes, 38, 20));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void ContainerPointsToAnOpfThatParses(int version)
    {
        using var archive = new ZipArchive(new MemoryStream(EpubWriter.Write(Spec(version))), ZipArchiveMode.Read);
        var container = XDocument.Parse(ReadEntry(archive, "META-INF/container.xml"));
        var path = container.Descendants().First(e => e.Name.LocalName == "rootfile").Attribute("full-path")!.Value;
        var opf = XDocument.Parse(ReadEntry(archive, path));
        XNamespace dc = "http://purl.org/dc/elements/1.1/";
        Assert.Equal("Fish & Chips <1>", opf.Descendants(dc + "title").Single().Value);
        Assert.Equal(version == 3, archive.GetEntry("OEBPS/nav.xhtml") is not null);
        Assert.Equal(version == 2, archive.GetEntry("OEBPS/toc.ncx") is not null);
        Assert.Equal(version == 3, opf.ToString().Contains("dcterms:modified", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public async Task VersOneReadsTheGeneratedEpub(int version, bool cover)
    {
        var spec = Spec(version, cover);
        var book = await ReadAsync(EpubWriter.Write(spec));
        Assert.Equal("Fish & Chips <1>", book.Title);
        Assert.Equal(["Martin Fowler", "Ana Núñez"], book.AuthorList);
        Assert.Contains(book.Schema.Package.Metadata.Identifiers, i => i.Identifier.Contains("9780000000019", StringComparison.Ordinal));
        Assert.Equal(spec.Document.Chapters.Count, book.ReadingOrder.Count);
        var firstWords = string.Join(' ', spec.Document.Chapters[0].Paragraphs[0].Split(' ').Take(20));
        Assert.Contains(System.Net.WebUtility.HtmlDecode(firstWords), System.Net.WebUtility.HtmlDecode(book.ReadingOrder[0].Content), StringComparison.Ordinal);
        Assert.Equal(cover, book.CoverImage is { Length: > 0 });
    }

    [Fact]
    public async Task VersOneReadsAnEpubWithoutIsbnAndWithDeflate()
    {
        var spec = new EpubSpec { Version = 3, Title = "Plain", Document = Document("de"), Deflate = true, Language = "de" };
        var book = await ReadAsync(EpubWriter.Write(spec));
        Assert.Equal("Plain", book.Title);
        Assert.Contains(book.Schema.Package.Metadata.Identifiers, i => i.Identifier.StartsWith("urn:uuid:", StringComparison.Ordinal));
    }

    [Fact]
    public void EpubOutputIsDeterministic()
    {
        Assert.True(EpubWriter.Write(Spec(3, true)).AsSpan().SequenceEqual(EpubWriter.Write(Spec(3, true))));
    }

    [Fact]
    public void MalformedOpfIsNotWellFormed()
    {
        using var archive = new ZipArchive(new MemoryStream(EpubWriter.Write(new EpubSpec { Version = 3, Title = "x", Document = Document(), MalformedOpf = true })), ZipArchiveMode.Read);
        Assert.ThrowsAny<System.Xml.XmlException>(() => XDocument.Parse(ReadEntry(archive, "OEBPS/content.opf")));
    }

    [Fact]
    public void PngHasValidSignatureHeaderAndChunkCrcs()
    {
        var png = PngWriter.Write(64, 96, 1, 2, 3);
        Assert.True(png.AsSpan().StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }));
        var offset = 8;
        var chunks = new List<string>();
        byte[] idat = [];
        while (offset < png.Length)
        {
            var length = (int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(offset, 4));
            var type = Encoding.ASCII.GetString(png, offset + 4, 4);
            var typeAndData = png.AsSpan(offset + 4, 4 + length);
            Assert.Equal(Crc32.Compute(typeAndData), BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(offset + 8 + length, 4)));
            if (type == "IHDR")
            {
                Assert.Equal(64u, BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(offset + 8, 4)));
                Assert.Equal(96u, BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(offset + 12, 4)));
                Assert.Equal((byte)8, png[offset + 16]);
                Assert.Equal((byte)2, png[offset + 17]);
            }

            if (type == "IDAT")
            {
                idat = png.AsSpan(offset + 8, length).ToArray();
            }

            chunks.Add(type);
            offset += 12 + length;
        }

        Assert.Equal(["IHDR", "IDAT", "IEND"], chunks);
        using var inflated = new MemoryStream();
        using (var zlib = new ZLibStream(new MemoryStream(idat), CompressionMode.Decompress))
        {
            zlib.CopyTo(inflated);
        }

        var raw = inflated.ToArray();
        Assert.Equal(96 * (1 + (64 * 3)), raw.Length);
        for (var row = 0; row < 96; row++)
        {
            Assert.Equal(0, raw[row * (1 + (64 * 3))]);
        }
    }

    [Fact]
    public void PdfStructureHasExactXrefOffsets()
    {
        var bytes = PdfWriter.Write(new PdfSpec
        {
            Pages = [["Hello (world) \\ test"], ["Second page"]],
            HasInfo = true,
            Title = "T",
            Author = "A",
        });
        var text = Encoding.Latin1.GetString(bytes);
        Assert.StartsWith("%PDF-1.7\n", text, StringComparison.Ordinal);
        var startxref = long.Parse(text[(text.LastIndexOf("startxref\n", StringComparison.Ordinal) + 10)..].Split('\n')[0]);
        Assert.StartsWith("xref\n", text[(int)startxref..], StringComparison.Ordinal);
        var lines = text[(int)startxref..].Split('\n');
        var size = int.Parse(lines[1].Split(' ')[1]);
        for (var n = 1; n < size; n++)
        {
            var entry = lines[2 + n];
            Assert.Equal(19, entry.Length);
            var offset = int.Parse(entry[..10]);
            Assert.StartsWith($"{n} 0 obj", text[offset..], StringComparison.Ordinal);
        }

        Assert.Contains("Hello \\(world\\) \\\\ test", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Utf16TitleIsWrittenAsHexStringWithBom()
    {
        Assert.Equal("(Plain \\(x\\))", PdfWriter.TextString("Plain (x)"));
        var hex = PdfWriter.TextString("Jördis");
        Assert.StartsWith("<FEFF", hex, StringComparison.Ordinal);
        var bytes = Convert.FromHexString(hex.Trim('<', '>'));
        Assert.Equal("Jördis", Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void PdfPigReadsTheGeneratedPdf(bool info, bool xmp)
    {
        var lines = new List<string> { "Größenordnungen verteilter Systeme", "by Lukas Weiß", string.Empty, "Erste Zeile des Textes" };
        var bytes = PdfWriter.Write(new PdfSpec
        {
            Pages = [lines, ["Zweite Seite"]],
            HasInfo = info,
            Title = info ? "Größenordnungen verteilter Systeme" : null,
            Author = info ? "Lukas Weiß" : null,
            HasXmp = xmp,
        });
        using var document = PdfDocument.Open(bytes);
        Assert.Equal(2, document.NumberOfPages);
        var page = document.GetPage(1).Text;
        Assert.StartsWith("Größenordnungen verteilter Systeme", page, StringComparison.Ordinal);
        Assert.Contains("Lukas Weiß", page, StringComparison.Ordinal);
        if (info)
        {
            Assert.Equal("Größenordnungen verteilter Systeme", document.Information.Title);
            Assert.Equal("Lukas Weiß", document.Information.Author);
        }
        else
        {
            Assert.True(string.IsNullOrEmpty(document.Information.Title));
        }

        Assert.Equal(xmp, document.TryGetXmpMetadata(out var metadata) && metadata is not null);
        if (xmp)
        {
            Assert.NotNull(metadata!.GetXDocument());
        }
    }

    [Fact]
    public void ImageOnlyPageHasNoLettersAndOneImage()
    {
        var bytes = DerivedFixtures.Create("scanned-page.pdf");
        using var document = PdfDocument.Open(bytes);
        var page = document.GetPage(1);
        Assert.Equal(1, document.NumberOfPages);
        Assert.Empty(page.Letters);
        Assert.Single(page.GetImages());
        Assert.Equal("Scanned Page", document.Information.Title);
    }

    private static string ReadEntry(ZipArchive archive, string name)
    {
        using var reader = new StreamReader(archive.GetEntry(name)!.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static async Task<EpubBook> ReadAsync(byte[] bytes) =>
        await EpubReader.ReadBookAsync(new MemoryStream(bytes));
}
