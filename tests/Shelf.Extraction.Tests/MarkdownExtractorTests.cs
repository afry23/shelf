using Xunit;

namespace Shelf.Extraction.Tests;

public sealed class MarkdownExtractorTests : IDisposable
{
    private readonly string _folder = Directory.CreateTempSubdirectory("shelf-md-").FullName;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private ExtractedBook Read(string markdown, bool withText = true)
    {
        var path = Path.Combine(_folder, "article.md");
        File.WriteAllText(path, markdown);
        return BookExtractor.Extract(path);
    }

    [Fact]
    public void The_first_top_level_heading_is_the_title()
    {
        var book = Read("Intro\n\n# Why Tests Matter\n\nText.\n\n# Another\n");

        Assert.Equal("Why Tests Matter", book.Metadata.Title);
        Assert.Equal(MetadataOrigin.FileContent, book.Metadata.TitleOrigin);
    }

    [Fact]
    public void The_text_is_cut_at_the_headings_and_placed_by_slug()
    {
        var book = Read("Before.\n\n# Title\n\nFirst.\n\n## Same Name\n\nSecond.\n\n## Same Name\n\nThird.\n");

        Assert.Equal(["heading=start", "heading=title", "heading=same-name", "heading=same-name-2"], book.Segments.Select(s => s.Locator));
        Assert.Contains("Second.", book.Segments[2].Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Links_and_images_count_as_their_text()
    {
        var book = Read("# T\n\nSee [the docs](https://example.com/docs) and ![a harbour](images/01-3fa9c2d1.jpg).\n");

        var text = string.Join(' ', book.Segments.Select(s => s.Text));
        Assert.Contains("the docs", text, StringComparison.Ordinal);
        Assert.Contains("a harbour", text, StringComparison.Ordinal);
        Assert.DoesNotContain("example.com", text, StringComparison.Ordinal);
        Assert.DoesNotContain("3fa9c2d1", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_heading_inside_a_code_fence_is_code()
    {
        var book = Read("# T\n\n```\n# not a heading\n```\n");

        Assert.Equal(["heading=t"], book.Segments.Select(s => s.Locator));
    }

    [Fact]
    public void A_file_without_a_heading_takes_its_name_as_title()
    {
        var book = Read("Just text.\n");

        Assert.Equal("article", book.Metadata.Title);
        Assert.Equal(MetadataOrigin.FileName, book.Metadata.TitleOrigin);
    }
}
