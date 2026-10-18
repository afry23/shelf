using System.IO.Compression;
using System.Text;
using Reqnroll;
using Shelf.Core.Model;
using Shelf.Extraction;
using Shelf.TestData;
using Xunit;

namespace Shelf.Specs.Steps;

[Binding]
public sealed class ExtractionSteps(ConfigSteps workspace)
{
    private ExtractedBook? _book;
    private ExtractionException? _error;
    private string? _path;
    private string? _isbn;
    private string? _locator;
    private int? _page;
    private FileNameMetadata.Guess? _guess;

    // ---- Given ----

    [Given("a file {string} with the content nothing")]
    public void GivenAFileWithNoContent(string name) => File.WriteAllBytes(WorkspacePath(name), []);

    [Given("a file {string} with the content the text {string}")]
    public void GivenAFileWithTheText(string name, string text) => File.WriteAllText(WorkspacePath(name), text, new UTF8Encoding(false));

    [Given("the fixture {string} cut off after half of its bytes as {string}")]
    public void GivenTheFixtureCutOff(string fixture, string name)
    {
        var bytes = File.ReadAllBytes(FixturePath(fixture));
        File.WriteAllBytes(WorkspacePath(name), bytes[..(bytes.Length / 2)]);
    }

    [Given("the fixture {string} copied as {string}")]
    public void GivenTheFixtureCopiedAs(string fixture, string name)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(WorkspacePath(name))!);
        File.Copy(FixturePath(fixture), WorkspacePath(name));
    }

    [Given("the fixture {string} copied as {string} with a trailing line break")]
    public void GivenTheFixtureCopiedWithATrailingLineBreak(string fixture, string name)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(WorkspacePath(name))!);
        // Readers ignore bytes after the end-of-file marker: another hash, the same book.
        File.WriteAllBytes(WorkspacePath(name), [.. File.ReadAllBytes(FixturePath(fixture)), (byte)'\n']);
    }

    [Given("an EPUB {string} whose chapter claims to have {int} MiB when unpacked")]
    public void GivenAnEpubWithAHugeEntry(string name, int mebibytes)
    {
        // Zeros compress to almost nothing: the file stays small while the central directory declares the full size.
        using var file = File.Create(WorkspacePath(name));
        using var archive = new ZipArchive(file, ZipArchiveMode.Create);
        var entry = archive.CreateEntry("OEBPS/chapter1.xhtml", CompressionLevel.SmallestSize);
        using var stream = entry.Open();
        var block = new byte[1024 * 1024];
        for (var i = 0; i < mebibytes; i++)
        {
            stream.Write(block);
        }
    }

    // ---- When ----

    [When("shelf extracts the fixture {string}")]
    public void WhenShelfExtractsTheFixture(string fixture) => Extract(FixturePath(fixture), ExtractionOptions.Default);

    [When("shelf extracts the fixture {string} without using its file name")]
    public void WhenShelfExtractsTheFixtureWithoutItsFileName(string fixture) =>
        Extract(FixturePath(fixture), ExtractionOptions.Default with { FileNameFallback = false });

    [When("shelf extracts the file {string}")]
    public void WhenShelfExtractsTheFile(string name) => Extract(WorkspacePath(name), ExtractionOptions.Default);

    [When("shelf reads the file name {string}")]
    public void WhenShelfReadsTheFileName(string name) => _guess = FileNameMetadata.Parse(name);

    [When("shelf searches the text {string} for an ISBN")]
    public void WhenShelfSearchesTheText(string text) => _isbn = Isbn.Find(text);

    [When("shelf reads the identifier {string}")]
    public void WhenShelfReadsTheIdentifier(string identifier) => _isbn = Isbn.FromIdentifier(identifier);

    [When("shelf builds the chapter locator for spine item {int} with the id {string}")]
    public void WhenShelfBuildsTheChapterLocator(int index, string idref) => _locator = Locators.EpubChapter(index, idref);

    [When("shelf parses the page locator {string}")]
    public void WhenShelfParsesThePageLocator(string locator) =>
        _page = Locators.TryParsePdfPage(locator, out var page) ? page : null;

    // ---- Then: metadata ----

    [Then("the title is {string}")]
    public void ThenTheTitleIs(string title) => Assert.Equal(title, Metadata().Title);

    [Then("there is no title")]
    public void ThenThereIsNoTitle() => Assert.Null(Metadata().Title);

    [Then("the authors are {string}")]
    public void ThenTheAuthorsAre(string authors) =>
        Assert.Equal(authors.Length == 0 ? [] : authors.Split("; "), Metadata().Authors);

    [Then("there are no authors")]
    public void ThenThereAreNoAuthors() => Assert.Empty(Metadata().Authors);

    [Then("the language is {string}")]
    public void ThenTheLanguageIs(string language) => Assert.Equal(language.Length == 0 ? null : language, Metadata().Language);

    [Then("the year is {string}")]
    public void ThenTheYearIs(string year) =>
        Assert.Equal(year.Length == 0 ? null : int.Parse(year, System.Globalization.CultureInfo.InvariantCulture), Metadata().Year);

    [Then("the series is {string} with the volume {int}")]
    public void ThenTheSeriesIs(string series, int volume)
    {
        Assert.Equal(series, Metadata().Series);
        Assert.Equal((double)volume, Metadata().SeriesIndex);
    }

    [Then("the page count is {int}")]
    public void ThenThePageCountIs(int pages) => Assert.Equal(pages, Book().PageCount);

    [Then("the title and authors come from the file content")]
    public void ThenTheTitleAndAuthorsComeFromTheFileContent()
    {
        Assert.Equal(MetadataOrigin.FileContent, Metadata().TitleOrigin);
        Assert.Equal(MetadataOrigin.FileContent, Metadata().AuthorsOrigin);
    }

    [Then(@"^the title comes from the (file content|file name|nothing)$")]
    public void ThenTheTitleComesFrom(string origin) => Assert.Equal(Origin(origin), Metadata().TitleOrigin);

    [Then(@"^the authors come from the (file content|file name|nothing)$")]
    public void ThenTheAuthorsComeFrom(string origin) => Assert.Equal(Origin(origin), Metadata().AuthorsOrigin);

    // ---- Then: ISBN ----

    [Then("the ISBN is {string}")]
    public void ThenTheIsbnIs(string isbn)
    {
        var actual = _isbn ?? (_book is not null ? _book.Metadata.Isbn13 : null);
        Assert.Equal(isbn.Length == 0 ? null : isbn, actual);
    }

    [Then("there is no ISBN")]
    public void ThenThereIsNoIsbn() => Assert.Null(_isbn);

    // ---- Then: file name guess ----

    [Then("the file name title is {string}")]
    public void ThenTheFileNameTitleIs(string title) => Assert.Equal(title, _guess!.Title);

    [Then("the file name authors are {string}")]
    public void ThenTheFileNameAuthorsAre(string authors) =>
        Assert.Equal(authors.Length == 0 ? [] : authors.Split("; "), _guess!.Authors);

    [Then("the file name year is {string}")]
    public void ThenTheFileNameYearIs(string year) =>
        Assert.Equal(year.Length == 0 ? null : int.Parse(year, System.Globalization.CultureInfo.InvariantCulture), _guess!.Year);

    // ---- Then: text and locators ----

    [Then("the text has {int} segments")]
    public void ThenTheTextHasSegments(int count) => Assert.Equal(count, Book().Segments.Count);

    [Then("the locator of segment {int} is {string}")]
    public void ThenTheLocatorOfSegmentIs(int number, string locator) => Assert.Equal(locator, Book().Segments[number - 1].Locator);

    [Then("the locator of the last segment is {string}")]
    public void ThenTheLocatorOfTheLastSegmentIs(string locator) => Assert.Equal(locator, Book().Segments[^1].Locator);

    [Then("the text of segment {int} contains {string}")]
    public void ThenTheTextOfSegmentContains(int number, string text) =>
        Assert.Contains(text, Book().Segments[number - 1].Text, StringComparison.Ordinal);

    [Then("the locator is {string}")]
    public void ThenTheLocatorIs(string locator) => Assert.Equal(locator, _locator);

    [Then("the page is {string}")]
    public void ThenThePageIs(string page) =>
        Assert.Equal(page.Length == 0 ? null : int.Parse(page, System.Globalization.CultureInfo.InvariantCulture), _page);

    // ---- Then: errors ----

    [Then("extraction fails with the kind {string}")]
    public void ThenExtractionFailsWithTheKind(string kind)
    {
        Assert.NotNull(_error);
        Assert.Equal(kind, _error.KindName);
    }

    [Then("the error message starts with the path of the file")]
    public void ThenTheErrorMessageStartsWithThePath()
    {
        Assert.NotNull(_error);
        Assert.StartsWith(_path + ": ", _error.Message, StringComparison.Ordinal);
    }

    [Then("extraction ends with a result or with an error of the kind {string}")]
    public void ThenExtractionEndsWithAResultOrAnError(string kind)
    {
        if (_error is not null)
        {
            Assert.Equal(kind, _error.KindName);
        }
        else
        {
            Assert.NotNull(_book);
        }
    }

    [Then("the file {string} is identical to the fixture {string}")]
    public void ThenTheFileIsIdenticalToTheFixture(string name, string fixture) =>
        Assert.Equal(File.ReadAllBytes(FixturePath(fixture)), File.ReadAllBytes(WorkspacePath(name)));

    // ---- helpers ----

    private void Extract(string path, ExtractionOptions options)
    {
        _path = path;
        try
        {
            _book = BookExtractor.Extract(path, options);
        }
        catch (ExtractionException e)
        {
            _error = e;
        }
    }

    private static MetadataOrigin Origin(string text) => text switch
    {
        "file content" => MetadataOrigin.FileContent,
        "file name" => MetadataOrigin.FileName,
        _ => MetadataOrigin.None,
    };

    private ExtractedBook Book()
    {
        Assert.Null(_error);
        return _book ?? throw new InvalidOperationException("Nothing was extracted in this scenario.");
    }

    private BookMetadata Metadata() => Book().Metadata;

    private static string FixturePath(string relative) =>
        Path.Combine(RepositoryPaths.Root(), "tests", "fixtures", relative);

    private string WorkspacePath(string name) => Path.Combine(workspace.WorkingDirectory, name);
}
