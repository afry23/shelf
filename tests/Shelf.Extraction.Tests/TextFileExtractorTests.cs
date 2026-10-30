using System.Text;
using Xunit;

namespace Shelf.Extraction.Tests;

public sealed class TextFileExtractorTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("shelf-txt-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private ExtractedBook Read(byte[] bytes)
    {
        var path = Path.Combine(_folder, "notes.txt");
        File.WriteAllBytes(path, bytes);
        return BookExtractor.Extract(path);
    }

    [Fact]
    public void The_text_is_cut_at_blank_lines_and_placed_by_line()
    {
        var book = Read(Encoding.UTF8.GetBytes("First note.\nSecond line.\n\n\nOther note.\r\n"));

        Assert.Equal(["line=1", "line=5"], book.Segments.Select(s => s.Locator));
        Assert.Equal(["First note.\nSecond line.", "Other note."], book.Segments.Select(s => s.Text.ReplaceLineEndings("\n")));
    }

    [Fact]
    public void A_byte_order_mark_is_dropped()
    {
        var book = Read([.. Encoding.UTF8.Preamble.ToArray(), .. Encoding.UTF8.GetBytes("Grüße")]);

        Assert.Equal("Grüße", Assert.Single(book.Segments).Text);
    }

    [Fact]
    public void Bytes_that_are_no_UTF8_are_read_as_Latin1()
    {
        var book = Read(Encoding.Latin1.GetBytes("Größe"));

        Assert.Equal("Größe", Assert.Single(book.Segments).Text);
    }

    [Theory]
    [InlineData("")]
    [InlineData("\n\n  \n")]
    public void A_file_without_text_has_no_segments(string content)
    {
        Assert.Empty(Read(Encoding.UTF8.GetBytes(content)).Segments);
    }

    [Theory]
    [InlineData("a.txt", true, false)]
    [InlineData("a.pdf", true, true)]
    [InlineData("a.epub", false, true)]
    [InlineData("a.docx", false, false)]
    public void Only_a_PDF_or_TXT_file_is_a_notes_file_and_a_TXT_file_is_no_book(string name, bool notes, bool book)
    {
        Assert.Equal(notes, BookExtractor.IsNotesFile(name));
        Assert.Equal(book, BookExtractor.IsSupported(name));
    }
}
