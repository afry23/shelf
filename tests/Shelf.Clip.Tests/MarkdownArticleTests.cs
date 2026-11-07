using Xunit;

namespace Shelf.Clip.Tests;

public sealed class MarkdownArticleTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("shelf-md-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void The_front_matter_gives_the_metadata()
    {
        var article = MarkdownArticle.Read("---\ntitle: T\nsource: https://a.example/x?utm_source=y#z\nauthor: \"[[A B]]\"\npublished: 2024-03-05\nsite: S\n---\nText", "f.md", _directory);
        Assert.Equal("T", article.Title);
        Assert.Equal("https://a.example/x", article.SourceUrl);
        Assert.Equal(["A B"], article.Authors);
        Assert.Equal(2024, article.Published?.Year);
        Assert.Equal("S", article.Site);
        Assert.Equal("Text\n", article.Markdown);
    }

    [Theory]
    [InlineData("# Heading\n\nx", "Heading")]
    [InlineData("no heading", "my-file")]
    public void The_title_falls_back_to_the_heading_then_the_file_name(string text, string title) =>
        Assert.Equal(title, MarkdownArticle.Read(text, "my-file.md", _directory).Title);

    [Fact]
    public void A_link_that_leaves_the_folder_is_not_followed()
    {
        var outer = Directory.CreateTempSubdirectory("shelf-md-outer-").FullName;
        try
        {
            File.WriteAllBytes(Path.Combine(outer, "secret.jpg"), Pictures.Jpeg(100, 100));
            var inner = Directory.CreateDirectory(Path.Combine(outer, "in")).FullName;
            var article = MarkdownArticle.Read("![x](../secret.jpg)", "f.md", inner);
            Assert.Empty(article.Images);
            Assert.Contains("![x](../secret.jpg)", article.Markdown, StringComparison.Ordinal);
            Assert.Contains(article.Warnings, w => w.Contains("outside", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(outer, recursive: true);
        }
    }

    [Fact]
    public void An_absolute_path_is_not_followed()
    {
        var article = MarkdownArticle.Read("![x](/etc/hosts)", "f.md", _directory);
        Assert.Empty(article.Images);
        Assert.Single(article.Warnings);
    }

    [Fact]
    public void An_obsidian_embed_is_found_in_a_subfolder_and_linked_relative()
    {
        Directory.CreateDirectory(Path.Combine(_directory, "attachments"));
        File.WriteAllBytes(Path.Combine(_directory, "attachments", "p.png"), Pictures.Png(10, 10));
        var article = MarkdownArticle.Read("![[p.png|Alt]] and ![[note.md]]", "f.md", _directory);
        var image = Assert.Single(article.Images);
        Assert.EndsWith(".png", image.FileName, StringComparison.Ordinal);
        Assert.Contains($"![Alt](images/{image.FileName})", article.Markdown, StringComparison.Ordinal);
        Assert.Contains("![[note.md]]", article.Markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void Remote_images_stay_links()
    {
        var article = MarkdownArticle.Read("![r](https://c.example/a.jpg) ![d](data:image/png;base64,AAAA)", "f.md", _directory);
        Assert.Empty(article.Images);
        Assert.Empty(article.Warnings);
    }
}
