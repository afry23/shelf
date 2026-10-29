using Xunit;

namespace Shelf.Clip.Tests;

public sealed class ArticleClipperTests
{
    private const string Post = """
        <html lang="en"><head>
          <title>Why Tests Matter | Example Blog</title>
          <meta property="og:title" content="Why Tests Matter">
          <meta property="og:site_name" content="Example Blog">
          <meta name="author" content="Jane Doe">
          <meta property="article:published_time" content="2024-03-05T09:00:00Z">
          <link rel="canonical" href="https://blog.example.com/why-tests-matter?utm_source=feed">
        </head><body>
          <nav>Home About Subscribe</nav>
          <article>
            <h1>Why Tests Matter</h1>
            <p>Tests are <strong>executable</strong> documentation, and they keep a codebase honest over many years of change.</p>
            <h2>Reasons</h2>
            <ul><li>They fail loudly</li><li>They document intent</li></ul>
            <blockquote>Code without tests is broken by design.</blockquote>
            <pre><code>dotnet test</code></pre>
            <p>One more paragraph so that the scoring has enough text to decide what the main content of this page is.</p>
          </article>
          <script>trackEverything()</script>
        </body></html>
        """;

    private static string Essay(string imageMarkup) => $"""
        <html><head><title>Photo Essay</title></head><body><article><h1>Photo Essay</h1>
        <p>The harbour wakes before the town does, and the boats leave while the light is still grey over the water and the gulls.</p>
        {imageMarkup}
        <p>By noon the nets are back and the quay smells of salt, diesel and fried fish, as it has done for a hundred years.</p>
        </article></body></html>
        """;

    private static Func<string, byte[]?> Files(params (string Address, byte[] Bytes)[] files) =>
        address => files.Where(f => f.Address == address).Select(f => f.Bytes).FirstOrDefault();

    [Fact]
    public void The_text_becomes_markdown_and_the_page_furniture_is_dropped()
    {
        var article = ArticleClipper.Clip(Post, null, Files());

        Assert.Equal("Why Tests Matter", article.Title);
        Assert.Equal(["Jane Doe"], article.Authors);
        Assert.Equal("Example Blog", article.Site);
        Assert.Equal(new DateTimeOffset(2024, 3, 5, 9, 0, 0, TimeSpan.Zero), article.Published);
        Assert.Equal("https://blog.example.com/why-tests-matter", article.SourceUrl);
        Assert.Equal("en", article.Language);
        Assert.StartsWith("# Why Tests Matter\n", article.Markdown, StringComparison.Ordinal);
        Assert.Contains("Tests are **executable** documentation", article.Markdown, StringComparison.Ordinal);
        Assert.Contains("## Reasons", article.Markdown, StringComparison.Ordinal);
        Assert.Contains("- They fail loudly", article.Markdown, StringComparison.Ordinal);
        Assert.Contains("> Code without tests is broken by design.", article.Markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("Subscribe", article.Markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("trackEverything", article.Markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void A_page_without_text_is_no_article() =>
        Assert.Throws<NoArticleException>(() => ArticleClipper.Clip("<html><body><nav>Menu</nav></body></html>", null, Files()));

    [Theory]
    [InlineData("""<img src="a.jpg" alt="Harbour">""", "a.jpg")]
    [InlineData("""<img src="s.jpg" srcset="s.jpg 400w, l.jpg 1600w" alt="Harbour">""", "l.jpg")]
    [InlineData("""<img src="s.jpg" srcset="s.jpg 1x, l.jpg 2x" alt="Harbour">""", "l.jpg")]
    [InlineData("""<img src="p.gif" data-src="real.jpg" alt="Harbour">""", "real.jpg")]
    [InlineData("""<figure><img src="a.jpg" alt="Harbour"><figcaption>Low tide</figcaption></figure>""", "a.jpg")]
    public void An_image_of_the_article_is_saved_and_linked_relative(string markup, string loaded)
    {
        var article = ArticleClipper.Clip(Essay(markup), null, Files((loaded, Pictures.Jpeg(1200, 800))));

        var image = Assert.Single(article.Images);
        Assert.Matches(@"^01-[0-9a-f]{8}\.jpg$", image.FileName);
        Assert.Contains("(images/" + image.FileName + ")", article.Markdown, StringComparison.Ordinal);
        Assert.Empty(article.Warnings);
    }

    [Theory]
    [InlineData("pixel.gif", 1, 1)]
    [InlineData("icon.png", 32, 32)]
    [InlineData("strip.png", 600, 10)]
    public void Icons_and_tracking_pixels_are_not_saved(string name, int width, int height)
    {
        var bytes = name.EndsWith(".gif", StringComparison.Ordinal) ? Pictures.Gif(width, height) : Pictures.Png(width, height);

        var article = ArticleClipper.Clip(Essay($"""<img src="{name}" alt="x">"""), null, Files((name, bytes)));

        Assert.Empty(article.Images);
        Assert.DoesNotContain("![", article.Markdown, StringComparison.Ordinal);
        Assert.Empty(article.Warnings);
    }

    [Fact]
    public void The_same_picture_twice_is_saved_once()
    {
        var picture = Pictures.Jpeg(1200, 800);

        var article = ArticleClipper.Clip(
            Essay("""<img src="a.jpg" alt="One"><img src="copy.jpg" alt="Two">"""),
            null,
            Files(("a.jpg", picture), ("copy.jpg", picture)));

        var image = Assert.Single(article.Images);
        Assert.Equal(2, article.Markdown.Split("(images/" + image.FileName + ")").Length - 1);
    }

    [Fact]
    public void An_image_that_cannot_be_had_stays_a_link_and_is_reported()
    {
        var article = ArticleClipper.Clip(Essay("""<img src="https://cdn.example.com/gone.jpg" alt="Gone">"""), null, Files());

        Assert.Empty(article.Images);
        Assert.Contains("![Gone](https://cdn.example.com/gone.jpg)", article.Markdown, StringComparison.Ordinal);
        Assert.Equal(["image not saved: https://cdn.example.com/gone.jpg"], article.Warnings);
    }

    [Fact]
    public void Relative_addresses_are_resolved_against_the_page_address()
    {
        var asked = new List<string>();

        ArticleClipper.Clip(
            Essay("""<img src="/img/a.jpg" alt="Harbour">"""),
            "https://blog.example.com/posts/harbour",
            address =>
            {
                asked.Add(address);
                return Pictures.Jpeg(1200, 800);
            });

        Assert.Equal(["https://blog.example.com/img/a.jpg"], asked);
    }
}
