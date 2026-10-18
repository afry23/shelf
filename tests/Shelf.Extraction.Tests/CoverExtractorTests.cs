using System.IO.Compression;
using System.Text;
using SkiaSharp;
using Xunit;

namespace Shelf.Extraction.Tests;

public sealed class CoverExtractorTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("shelf-covers-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "shelf.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
    }

    private static string Fixture(params string[] parts) => Path.Combine([RepositoryRoot(), "tests", "fixtures", .. parts]);

    private string Write(string name, byte[] content)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllBytes(path, content);
        return path;
    }

    /// <summary>A PDF with pages of the given size in points, written by hand (PDFium needs no more than this).</summary>
    private static byte[] Pdf(int width, int height, int pages = 1)
    {
        var text = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        void Object(int number, string body)
        {
            offsets.Add(text.Length);
            text.Append(CultureInfo(number)).Append(" 0 obj\n").Append(body).Append("\nendobj\n");
        }

        Object(1, "<< /Type /Catalog /Pages 2 0 R >>");
        var kids = string.Join(' ', Enumerable.Range(0, pages).Select(i => CultureInfo(3 + i) + " 0 R"));
        Object(2, $"<< /Type /Pages /Kids [{kids}] /Count {pages} >>");
        for (var i = 0; i < pages; i++)
        {
            Object(3 + i, $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {width} {height}] >>");
        }

        var xref = text.Length;
        text.Append("xref\n0 ").Append(CultureInfo(offsets.Count + 1)).Append("\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            text.Append(offset.ToString("D10", System.Globalization.CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        }

        text.Append("trailer\n<< /Size ").Append(CultureInfo(offsets.Count + 1)).Append(" /Root 1 0 R >>\nstartxref\n").Append(CultureInfo(xref)).Append("\n%%EOF\n");
        return Encoding.ASCII.GetBytes(text.ToString());
    }

    private static string CultureInfo(int number) => number.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static SKImageInfo Decode(byte[] image)
    {
        using var bitmap = SKBitmap.Decode(image);
        Assert.NotNull(bitmap);
        return bitmap.Info;
    }

    // ---- PDF ----

    [Theory]
    [InlineData(612, 792, 463, 600)]
    [InlineData(595, 842, 423, 600)]
    [InlineData(792, 612, 600, 463)]
    [InlineData(842, 595, 600, 423)]
    [InlineData(500, 500, 600, 600)]
    [InlineData(100, 400, 150, 600)]
    public void A_PDF_gets_a_JPEG_of_600_pixels_on_the_longest_edge(int width, int height, int expectedWidth, int expectedHeight)
    {
        var cover = CoverExtractor.Read(Write("page.pdf", Pdf(width, height)));

        Assert.NotNull(cover);
        Assert.Equal("jpg", cover.Extension);
        Assert.Equal("jpg", CoverExtractor.ExtensionOf(cover.Content));
        var info = Decode(cover.Content);
        Assert.Equal(expectedWidth, info.Width);
        Assert.Equal(expectedHeight, info.Height);
    }

    [Fact]
    public void Only_the_first_page_is_rendered()
    {
        var cover = CoverExtractor.Read(Write("three.pdf", Pdf(612, 792, pages: 3)));

        Assert.Equal(463, Decode(cover!.Content).Width);
    }

    [Fact]
    public void The_same_page_gives_the_same_bytes_twice()
    {
        var path = Write("page.pdf", Pdf(612, 792));

        Assert.Equal(CoverExtractor.Read(path)!.Content, CoverExtractor.Read(path)!.Content);
    }

    [Theory]
    [InlineData("Event Streams in Practice.pdf", 463, 600)]
    [InlineData("scanned-page.pdf", 463, 600)]
    [InlineData("Jane Doe - Quiet Systems (2004).pdf", 463, 600)]
    public void The_fixture_PDFs_are_rendered(string name, int width, int height)
    {
        var info = Decode(CoverExtractor.Read(Fixture("derived", name))!.Content);

        Assert.Equal(width, info.Width);
        Assert.Equal(height, info.Height);
    }

    [Fact]
    public void The_arXiv_paper_is_A4()
    {
        var info = Decode(CoverExtractor.Read(Fixture("upstream", "pdf", "arxiv-2403.02174v1.pdf"))!.Content);

        Assert.Equal((423, 600), (info.Width, info.Height));
    }

    [Fact]
    public void A_PDF_without_pages_is_a_bad_PDF()
    {
        var error = Assert.Throws<ExtractionException>(() => CoverExtractor.Read(Write("zero.pdf", Pdf(612, 792, pages: 0))));

        Assert.Equal(ExtractionErrorKind.BadPdf, error.Kind);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("random")]
    [InlineData("truncated")]
    public void A_file_that_is_no_PDF_is_a_bad_PDF(string kind)
    {
        var bytes = kind switch
        {
            "empty" => [],
            "random" => Enumerable.Range(0, 2048).Select(i => (byte)((i * 31) % 251)).ToArray(),
            _ => Pdf(612, 792)[..60],
        };

        var error = Assert.Throws<ExtractionException>(() => CoverExtractor.Read(Write("bad.pdf", bytes)));

        Assert.Equal(ExtractionErrorKind.BadPdf, error.Kind);
        Assert.Contains("bad.pdf", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_file_that_is_not_there_is_an_io_error()
    {
        var error = Assert.Throws<ExtractionException>(() => CoverExtractor.Read(Path.Combine(_directory, "nothing.pdf")));

        Assert.Equal(ExtractionErrorKind.Io, error.Kind);
    }

    [Fact]
    public void A_file_of_another_kind_has_no_cover() => Assert.Null(CoverExtractor.Read(Write("notes.txt", "text"u8.ToArray())));

    // ---- EPUB ----

    private string EpubWithCover(byte[]? cover)
    {
        var path = Path.Combine(_directory, "book.epub");
        File.Copy(Fixture("derived", "epub2-isbn-cover.epub"), path);
        if (cover is not null)
        {
            using var archive = ZipFile.Open(path, ZipArchiveMode.Update);
            archive.GetEntry("OEBPS/images/cover.png")!.Delete();
            using var stream = archive.CreateEntry("OEBPS/images/cover.png").Open();
            stream.Write(cover);
        }

        return path;
    }

    [Fact]
    public void The_cover_of_an_EPUB_is_the_embedded_image_as_it_is()
    {
        var original = ZipFile.OpenRead(Fixture("derived", "epub2-isbn-cover.epub")).GetEntry("OEBPS/images/cover.png")!;
        using var stream = original.Open();
        using var copy = new MemoryStream();
        stream.CopyTo(copy);

        var cover = CoverExtractor.Read(Fixture("derived", "epub2-isbn-cover.epub"));

        Assert.NotNull(cover);
        Assert.Equal("png", cover.Extension);
        Assert.Equal(copy.ToArray(), cover.Content);
        Assert.Equal((64, 96), (Decode(cover.Content).Width, Decode(cover.Content).Height));
    }

    [Theory]
    [InlineData("jpg", new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0 })]
    [InlineData("png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0 })]
    [InlineData("gif", new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0 })]
    [InlineData("webp", new byte[] { 0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x45, 0x42, 0x50 })]
    public void The_extension_comes_from_the_bytes_not_from_the_declared_type(string extension, byte[] bytes)
    {
        var cover = CoverExtractor.Read(EpubWithCover(bytes));

        Assert.NotNull(cover);
        Assert.Equal(extension, cover.Extension);
        Assert.Equal(bytes, cover.Content);
    }

    [Fact]
    public void An_image_of_an_unknown_kind_is_no_cover() =>
        Assert.Null(CoverExtractor.Read(EpubWithCover("this is not an image"u8.ToArray())));

    [Fact]
    public void An_EPUB_without_a_cover_has_none() =>
        Assert.Null(CoverExtractor.Read(Fixture("derived", "Jane Doe - Quiet Systems (2004).epub")));

    [Fact]
    public void A_file_that_is_no_EPUB_is_an_error()
    {
        var error = Assert.Throws<ExtractionException>(() => CoverExtractor.Read(Fixture("derived", "not-a-zip.epub")));

        Assert.Equal(ExtractionErrorKind.NotAContainer, error.Kind);
    }

    [Theory]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF }, "jpg")]
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38 }, null)]
    [InlineData(new byte[] { 0xFF, 0xD8 }, null)]
    [InlineData(new byte[] { }, null)]
    public void The_kind_of_an_image_is_read_from_its_first_bytes(byte[] bytes, string? expected) =>
        Assert.Equal(expected, CoverExtractor.ExtensionOf(bytes));
}
