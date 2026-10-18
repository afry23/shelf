using Xunit;

namespace Shelf.Extraction.Tests;

public sealed class TocExtractorTests
{
    private static string Fixture(params string[] parts) =>
        Path.Combine([RepositoryRoot(), "tests", "fixtures", .. parts]);

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "shelf.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("shelf.slnx not found");
    }

    [Fact]
    public void An_epub_gives_its_navigation_with_the_chapter_locations()
    {
        var toc = TocExtractor.Read(Fixture("upstream", "gutenberg", "pg201-flatland.epub3.epub"));

        Assert.NotEmpty(toc);
        Assert.All(toc, e => Assert.False(string.IsNullOrWhiteSpace(e.Title)));
        Assert.Contains(toc, e => e.Locator is not null && e.Locator.StartsWith("epubcfi(/6/", StringComparison.Ordinal));
        Assert.All(toc, e => Assert.InRange(e.Level, 1, 10));
    }

    [Fact]
    public void A_pdf_gives_its_outline_with_pages()
    {
        var toc = TocExtractor.Read(Fixture("upstream", "pdf", "arxiv-2403.02174v1.pdf"));

        Assert.NotEmpty(toc);
        Assert.Contains(toc, e => e.Locator is not null && e.Locator.StartsWith("page=", StringComparison.Ordinal));
    }

    [Fact]
    public void A_pdf_without_an_outline_gives_an_empty_list() =>
        Assert.Empty(TocExtractor.Read(Fixture("derived", "Jane Doe - Quiet Systems (2004).pdf")));

    [Theory]
    [InlineData("does-not-exist.epub")]
    [InlineData("notes.txt")]
    public void A_file_that_cannot_be_read_gives_an_empty_list(string name) =>
        Assert.Empty(TocExtractor.Read(Path.Combine(Path.GetTempPath(), name)));

    [Fact]
    public void A_file_that_is_no_epub_gives_an_empty_list() =>
        Assert.Empty(TocExtractor.Read(Fixture("derived", "not-a-zip.epub")));
}
