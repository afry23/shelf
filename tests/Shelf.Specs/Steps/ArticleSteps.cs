using System.Net;
using System.Text;
using Reqnroll;
using Shelf.Core.Model;
using Shelf.Index;
using Xunit;

namespace Shelf.Specs.Steps;

/// <summary>Steps of the article features (F-ART-*): saved pages, a fake web, and the folder of an article.</summary>
[Binding]
public sealed class ArticleSteps(ConfigSteps workspace, ScenarioEnvironment environment, BrowserSteps browser)
{
    private const string Library = "library";

    private const string Filler = "The harbour wakes before the town does, and the boats leave while the light is still grey over the water and the gulls.";

    private string LibraryRoot => Path.Combine(workspace.WorkingDirectory, Library);

    // ---- saved pages ----

    [Given("a saved web page {string} with content:")]
    public void GivenASavedWebPage(string name, string content) => WritePage(name, content);

    [Given("""^a saved web page "(.*)" with the article "(.*)" and the image markup (.*)$""")]
    public void GivenASavedWebPageWithImages(string name, string title, string markup) => WritePage(name, Essay(title, markup));

    [Given("""^the image "(.*)" of the saved page "(.*)" is a (\d+) by (\d+) picture$""")]
    public void GivenTheImageIsAPicture(string image, string page, int width, int height)
    {
        var path = Path.Combine(Path.GetDirectoryName(Path.Combine(workspace.WorkingDirectory, page))!, image);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, image.EndsWith(".gif", StringComparison.Ordinal) ? Picture.Gif(width, height) : Picture.Jpeg(width, height, (byte)image.GetHashCode(StringComparison.Ordinal)));
    }

    [Given("the image {string} of the saved page {string} is the same picture as {string}")]
    public void GivenTheImageIsTheSamePicture(string image, string page, string other)
    {
        var directory = Path.GetDirectoryName(Path.Combine(workspace.WorkingDirectory, page))!;
        File.Copy(Path.Combine(directory, other), Path.Combine(directory, image), overwrite: true);
    }

    [Given("a saved MHTML page {string} with the article {string} and one {int} by {int} image")]
    public void GivenASavedMhtmlPage(string name, string title, int width, int height)
    {
        var html = Essay(title, """<img src="https://cdn.example.com/harbour.jpg" alt="Harbour">""");
        var image = Convert.ToBase64String(Picture.Jpeg(width, height), Base64FormattingOptions.InsertLineBreaks);
        var mhtml = $"""
            From: <Saved by shelf tests>
            Subject: {title}
            MIME-Version: 1.0
            Content-Type: multipart/related; type="text/html"; boundary="----=_Part_1"

            ------=_Part_1
            Content-Type: text/html; charset="utf-8"
            Content-Transfer-Encoding: 8bit
            Content-Location: https://blog.example.com/harbour

            {html}
            ------=_Part_1
            Content-Type: image/jpeg
            Content-Transfer-Encoding: base64
            Content-Location: https://cdn.example.com/harbour.jpg

            {image}
            ------=_Part_1--
            """;
        WritePage(name, mhtml.Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    private void WritePage(string name, string content)
    {
        var path = Path.Combine(workspace.WorkingDirectory, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static string Essay(string title, string markup) => $"""
        <html><head><title>{title}</title></head><body><article><h1>{title}</h1>
        <p>{Filler}</p>
        {markup}
        <p>By noon the nets are back and the quay smells of salt, diesel and fried fish, as it has done for a hundred years.</p>
        </article></body></html>
        """;

    // ---- the fake web ----

    [Given("the online source {string} serves the article at {string} titled {string} by {string} with one image")]
    public void GivenTheWebServesAnArticle(string source, string url, string title, string author)
    {
        Assert.Equal("web", source);
        var origin = new Uri(url).GetLeftPart(UriPartial.Authority);
        environment.Online.Web.Serve(url, "text/html", Encoding.UTF8.GetBytes($"""
            <html lang="en"><head><title>{title} | Example Blog</title>
            <meta property="og:title" content="{title}"><meta property="og:site_name" content="Example Blog">
            <meta name="author" content="{author}"></head>
            <body><nav>Home About</nav><article><h1>{title}</h1><p>{Filler}</p>
            <img src="/images/photo.jpg" alt="Photo">
            <p>By noon the nets are back and the quay smells of salt, diesel and fried fish, as it has done for a hundred years.</p>
            </article></body></html>
            """));
        environment.Online.Web.Serve(origin + "/images/photo.jpg", "image/jpeg", Picture.Jpeg(1200, 800));
    }

    [Given("the online source {string} serves the same article also at {string}")]
    public void GivenTheWebServesTheSameArticleAlso(string source, string url)
    {
        Assert.Equal("web", source);
        environment.Online.Web.Alias(url);
    }

    // ---- articles on disk ----

    [Given("an article {string} from the site {string} with the text {string}")]
    public void GivenAnArticle(string title, string site, string text) => AddArticle(title, site, text, 0);

    [Given("an article {string} from the site {string} with the text {string} and {int} images")]
    public void GivenAnArticleWithImages(string title, string site, string text, int images) => AddArticle(title, site, text, images);

    [Given("an article {string} from the site {string} with the text {string} and {int} images and the ID {string}")]
    public void GivenAnArticleWithImagesAndId(string title, string site, string text, int images, string id) => AddArticle(title, site, text, images, Ulid.Parse(id));

    [Given("the article {string} has an image saved as {string}")]
    public void GivenTheArticleHasAnImage(string title, string path)
    {
        var folder = FolderOf(title);
        Directory.CreateDirectory(Path.Combine(folder, Path.GetDirectoryName(path)!));
        File.WriteAllBytes(Path.Combine(folder, path), Picture.Jpeg(1200, 800));
        File.AppendAllText(Path.Combine(folder, LibraryArticles.ArticleFile), $"\n![]({path})\n");
        LibraryReindexer.Rebuild(LibraryRoot);
    }

    [Given("the folder of the article {string} is renamed to {string}")]
    public void GivenTheFolderIsRenamed(string title, string folder)
    {
        var target = Path.Combine(LibraryRoot, folder);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        Directory.Move(FolderOf(title), target);
        LibraryReindexer.Rebuild(LibraryRoot);
    }

    [Given("the article {string} also contains {string}")]
    public void GivenTheArticleAlsoContains(string title, string text) =>
        File.AppendAllText(Path.Combine(FolderOf(title), LibraryArticles.ArticleFile), "\n" + text + "\n");

    private void AddArticle(string title, string site, string text, int images, Ulid? id = null)
    {
        var folder = Path.Combine(LibraryRoot, site, title);
        Directory.CreateDirectory(folder);
        var markdown = new StringBuilder($"# {title}\n\n{text}\n");
        for (var i = 1; i <= images; i++)
        {
            var name = $"{i:D2}-{i:D8}.jpg";
            Directory.CreateDirectory(Path.Combine(folder, LibraryArticles.ImagesFolder));
            File.WriteAllBytes(Path.Combine(folder, LibraryArticles.ImagesFolder, name), Picture.Jpeg(1200, 800, (byte)i));
            markdown.Append($"\n![Picture {i}]({LibraryArticles.ImagesFolder}/{name})\n");
        }

        File.WriteAllText(Path.Combine(folder, LibraryArticles.ArticleFile), markdown.ToString());
        SidecarStore.Write(Path.Combine(folder, SidecarStore.FileName), new WorkSidecar
        {
            Id = id ?? Ulid.New(),
            Title = title,
            Authors = [site],
            Kind = WorkKind.Article,
            Site = site,
            Files = [new SidecarFile(LibraryArticles.ArticleFile)],
        });
        LibraryReindexer.Rebuild(LibraryRoot);
    }

    // The trash and the journal below .shelf keep copies of sidecars; they are no works.
    private string FolderOf(string title) => Path.GetDirectoryName(Directory.EnumerateFiles(LibraryRoot, SidecarStore.FileName, SearchOption.AllDirectories)
        .Where(f => !Path.GetRelativePath(LibraryRoot, f).StartsWith(".shelf", StringComparison.Ordinal))
        .Single(f => SidecarStore.Read(f).Title == title))!;

    // ---- the reader ----

    [When("I open the article {string} in the reader")]
    public void WhenIOpenTheArticleInTheReader(string title) =>
        browser.WhenIOpen($"/read/{SidecarOf(title).Id}~{LibraryArticles.ArticleFile}");

    [Then("the reader shows {int} images")]
    public void ThenTheReaderShowsImages(int count) =>
        Assert.True(Poll(() => browser.Page.Locator(".article img").CountAsync().GetAwaiter().GetResult() == count), $"The reader does not show {count} images.");

    [Then("every image of the reader comes from the library")]
    public void ThenEveryImageComesFromTheLibrary()
    {
        var sources = browser.Page.Locator(".article img").EvaluateAllAsync<string[]>("images => images.map(i => i.getAttribute('src') ?? '')").GetAwaiter().GetResult();
        Assert.NotEmpty(sources);
        Assert.All(sources, s => Assert.StartsWith("/api/v1/files/", s, StringComparison.Ordinal));
    }

    [Then("the reader ran no script from the article")]
    public void ThenTheReaderRanNoScript()
    {
        Assert.Equal(0, browser.Page.Locator(".article script").CountAsync().GetAwaiter().GetResult());
        Assert.True(browser.Page.EvaluateAsync<bool>("() => window.pwned === undefined").GetAwaiter().GetResult());
    }

    [Then("the reader loaded no image from outside the library")]
    public void ThenTheReaderLoadedNoRemoteImage() =>
        Assert.Equal(0, browser.Page.Locator(".article img:not([src^='/api/v1/files/'])").CountAsync().GetAwaiter().GetResult());

    private static bool Poll(Func<bool> condition)
    {
        for (var i = 0; i < 100; i++)
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(100);
        }

        return condition();
    }

    // ---- assertions ----

    [Then("the work {string} has the site {string}")]
    public void ThenTheWorkHasTheSite(string title, string site) => Assert.Equal(site, Scalar("site", title));

    [Then("the work {string} has the source URL {string}")]
    public void ThenTheWorkHasTheSourceUrl(string title, string url) => Assert.Equal(url, Scalar("source_url", title));

    [Then("the sidecar of the work {string} has the kind {string}")]
    public void ThenTheSidecarHasTheKind(string title, string kind) =>
        Assert.Equal(kind, SidecarOf(title).Kind.ToString().ToLowerInvariant());

    [Then("the sidecar of the work {string} has the schema {int}")]
    public void ThenTheSidecarHasTheSchema(string title, int schema) => Assert.Equal(schema, SidecarOf(title).Schema);

    [Then("the article {string} contains {string}")]
    public void ThenTheArticleContains(string title, string text) => Assert.Contains(text, Markdown(title), StringComparison.Ordinal);

    [Then("the article {string} does not contain {string}")]
    public void ThenTheArticleDoesNotContain(string title, string text) => Assert.DoesNotContain(text, Markdown(title), StringComparison.Ordinal);

    [Then("the article {string} has {int} images in its folder")]
    public void ThenTheArticleHasImages(string title, int count)
    {
        var images = Path.Combine(FolderOf(title), LibraryArticles.ImagesFolder);
        Assert.Equal(count, Directory.Exists(images) ? Directory.GetFiles(images).Length : 0);
    }

    [Then("the article {string} links the same image twice")]
    public void ThenTheArticleLinksTheSameImageTwice(string title)
    {
        var links = System.Text.RegularExpressions.Regex.Matches(Markdown(title), @"\]\((images/[^)]+)\)").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(2, links.Count);
        Assert.Single(links.Distinct());
    }

    [Then("the article {string} is found at {string}")]
    public void ThenTheArticleIsFoundAt(string title, string folder) =>
        Assert.Equal(folder, Path.GetRelativePath(LibraryRoot, FolderOf(title)).Replace(Path.DirectorySeparatorChar, '/'));

    [Then("the trash holds the article {string} with {int} images")]
    public void ThenTheTrashHoldsTheArticle(string title, int count)
    {
        var trash = Path.Combine(LibraryRoot, ".shelf", "trash");
        var files = Directory.Exists(trash) ? Directory.EnumerateFiles(trash, "*", SearchOption.AllDirectories).ToList() : [];
        Assert.Contains(files, f => Path.GetFileName(f) == LibraryArticles.ArticleFile);
        Assert.Equal(count, files.Count(f => Path.GetFileName(Path.GetDirectoryName(f)!) == LibraryArticles.ImagesFolder));
    }

    private string Markdown(string title) => File.ReadAllText(Path.Combine(FolderOf(title), LibraryArticles.ArticleFile));

    private WorkSidecar SidecarOf(string title) => SidecarStore.Read(Path.Combine(FolderOf(title), SidecarStore.FileName));

    private object? Scalar(string column, string title)
    {
        Assert.Matches("^[a-z_]+$", column);
        using var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(LibraryRoot));
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {column} FROM works WHERE title = $v";
        command.Parameters.AddWithValue("$v", title);
        var value = command.ExecuteScalar();
        return value is DBNull ? null : value;
    }
}

/// <summary>Pages and images that <c>shelf clip</c> gets instead of the internet; every request is noted as a call of the source "web".</summary>
public sealed class FakeWeb(FakeOnline online, FakeSource source) : IHttpGetter
{
    private readonly Dictionary<string, (string Type, byte[] Body)> _served = new(StringComparer.Ordinal);
    private string? _last;

    public void Serve(string url, string contentType, byte[] body)
    {
        _served[url] = (contentType, body);
        _last = url;
    }

    /// <summary>The page served last is served at this address, too.</summary>
    public void Alias(string url) => _served[url] = _served[_last!];

    public HttpAnswer Get(string url, CancellationToken cancellation)
    {
        online.Calls.Add($"web:GET {url}");
        if (source.FailWith is { } failure)
        {
            return new HttpAnswer(int.TryParse(failure, out var status) ? (HttpStatusCode)status : HttpStatusCode.InternalServerError, "text/plain", []);
        }

        return _served.TryGetValue(url, out var found)
            ? new HttpAnswer(HttpStatusCode.OK, found.Type, found.Body)
            : new HttpAnswer(HttpStatusCode.NotFound, "text/plain", []);
    }
}

/// <summary>Header-only pictures: enough for <c>ImageProbe</c>, no decoder is involved.</summary>
internal static class Picture
{
    public static byte[] Jpeg(int width, int height, byte salt = 0) =>
        [0xFF, 0xD8, 0xFF, 0xC0, 0x00, 0x0B, 0x08, (byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width, 0x01, 0x01, 0x11, salt, 0xFF, 0xD9];

    public static byte[] Gif(int width, int height) =>
        [.. Encoding.ASCII.GetBytes("GIF89a"), (byte)width, (byte)(width >> 8), (byte)height, (byte)(height >> 8), 0, 0, 0];
}
