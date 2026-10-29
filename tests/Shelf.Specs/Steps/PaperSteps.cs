using Reqnroll;
using Shelf.Core.Grouping;
using Shelf.Core.Model;
using Shelf.Index;
using Shelf.TestData;
using Xunit;

namespace Shelf.Specs.Steps;

/// <summary>Steps of the paper features (F-PAP-*): identifier detection and the paper fields in the index.</summary>
[Binding]
public sealed class PaperSteps(ConfigSteps workspace)
{
    private const string Library = "library";

    private string? _doi;
    private string? _arxivId;
    private string? _citeKey;

    [Given("a sample PDF {string} with the title {string}, the author {string} and the first page text {string}")]
    public void GivenASamplePaperPdf(string name, string title, string author, string firstPageText)
    {
        var path = Path.Combine(workspace.WorkingDirectory, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, SampleBooks.PaperPdf(title, author, firstPageText));
    }

    [Given("a sample PDF {string} with the title {string}, the author {string}, the first page text {string} and the text seed {int}")]
    public void GivenASamplePaperPdfWithSeed(string name, string title, string author, string firstPageText, int seed)
    {
        var path = Path.Combine(workspace.WorkingDirectory, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, SampleBooks.PaperPdf(title, author, firstPageText, seed));
    }

    [Given("a BibTeX file {string} with content:")]
    public void GivenABibTeXFile(string name, string content)
    {
        var path = Path.Combine(workspace.WorkingDirectory, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content.Replace("\r\n", "\n", StringComparison.Ordinal) + "\n");
    }

    [When("shelf searches the text {string} for a DOI")]
    public void WhenShelfSearchesTheTextForADoi(string text) => _doi = PaperIds.FindDoi(text);

    [When("shelf searches the text {string} for an arXiv ID")]
    public void WhenShelfSearchesTheTextForAnArxivId(string text) => _arxivId = PaperIds.FindArxivId(text);

    [When("shelf builds a cite key for the title {string}, the authors {string} and the year {int}")]
    public void WhenShelfBuildsACiteKey(string title, string authors, int year) =>
        _citeKey = CiteKeys.Suggest(authors.Split(", "), year, title);

    [Then("the cite key is {string}")]
    public void ThenTheCiteKeyIs(string key) => Assert.Equal(key, _citeKey);

    [Then("the DOI is {string}")]
    public void ThenTheDoiIs(string doi) => Assert.Equal(doi, _doi);

    [Then("there is no DOI")]
    public void ThenThereIsNoDoi() => Assert.Null(_doi);

    [Then("the arXiv ID is {string}")]
    public void ThenTheArxivIdIs(string id) => Assert.Equal(id, _arxivId);

    [Then("there is no arXiv ID")]
    public void ThenThereIsNoArxivId() => Assert.Null(_arxivId);

    [Then("the work {string} has the kind {string}")]
    public void ThenTheWorkHasTheKind(string title, string kind) => Assert.Equal(kind, Scalar("kind", title));

    [Then("the work {string} has the arXiv ID {string}")]
    public void ThenTheWorkHasTheArxivId(string title, string id) => Assert.Equal(id, Scalar("arxiv_id", title));

    [Then("the work {string} has the DOI {string}")]
    public void ThenTheWorkHasTheDoi(string title, string doi) => Assert.Equal(doi, Scalar("doi", title));

    [Given("a paper {string} by {string} from {int} with the cite key {string}")]
    public void GivenAPaperWithTheCiteKey(string title, string authors, int year, string citeKey) =>
        AddPaper(new WorkSidecar { Title = title, Year = year, CiteKey = citeKey }, authors);

    [Given("a paper {string} by {string} without a year with the cite key {string}")]
    public void GivenAPaperWithoutAYear(string title, string authors, string citeKey) =>
        AddPaper(new WorkSidecar { Title = title, CiteKey = citeKey }, authors);

    [Given("a paper {string} by {string} from {int} without a cite key")]
    public void GivenAPaperWithoutACiteKey(string title, string authors, int year) =>
        AddPaper(new WorkSidecar { Title = title, Year = year }, authors);

    [Given("a paper {string} by {string} from {int} with the DOI {string} and the venue {string}")]
    public void GivenAPaperWithDoiAndVenue(string title, string authors, int year, string doi, string venue) =>
        AddPaper(new WorkSidecar { Title = title, Year = year, Doi = doi, Venue = venue }, authors);

    [Given("a paper {string} with the arXiv ID {string} and the title {string} and no authors")]
    public void GivenAPaperWithArxivId(string folder, string arxivId, string title) =>
        AddPaperAt(folder, new WorkSidecar { Title = title, ArxivId = arxivId }, [AuthorNames.UnknownAuthor]);

    [Given("a paper {string} with the DOI {string} and the title {string} and no authors")]
    public void GivenAPaperWithDoi(string folder, string doi, string title) =>
        AddPaperAt(folder, new WorkSidecar { Title = title, Doi = doi }, [AuthorNames.UnknownAuthor]);

    [Given("a paper {string} with the title {string} and the authors {string} and no identifier")]
    public void GivenAPaperWithoutIdentifier(string folder, string title, string authors) =>
        AddPaperAt(folder, new WorkSidecar { Title = title }, authors.Split(", "));

    [Given("a book {string} by {string} from {int}")]
    public void GivenABook(string title, string authors, int year) =>
        AddPaperAt(Path.Combine(authors.Split(", ")[0], title), new WorkSidecar { Title = title, Year = year }, authors.Split(", "), WorkKind.Book);

    [Given("the paper {string} has the abstract {string}")]
    public void GivenThePaperHasTheAbstract(string title, string text)
    {
        var root = Path.Combine(workspace.WorkingDirectory, Library);
        var path = Directory.EnumerateFiles(root, SidecarStore.FileName, SearchOption.AllDirectories)
            .Single(f => SidecarStore.Read(f).Title == title);
        SidecarStore.Write(path, SidecarStore.Read(path) with { Abstract = text });
        LibraryReindexer.Rebuild(root, includeContent: false);
    }

    private void AddPaper(WorkSidecar template, string authors)
    {
        var names = authors.Split(", ");
        AddPaperAt(Path.Combine(names[0], template.Title), template, names);
    }

    /// <summary>Writes the work with a sample PDF into <c>library/&lt;folder&gt;</c> and rebuilds the metadata of the index.</summary>
    private void AddPaperAt(string folderName, WorkSidecar template, string[] names, WorkKind kind = WorkKind.Paper)
    {
        var folder = Path.Combine(workspace.WorkingDirectory, Library, folderName);
        Directory.CreateDirectory(folder);
        var file = "paper.pdf";
        File.WriteAllBytes(Path.Combine(folder, file), SampleBooks.PaperPdf(template.Title, names[0], "A paper.", seed: (int)((uint)template.Title.GetHashCode(StringComparison.Ordinal) % 1000)));
        var sidecar = template with
        {
            Id = Ulid.New(),
            Kind = kind,
            Authors = names,
            Files = [new SidecarFile(file)],
        };
        SidecarStore.Write(Path.Combine(folder, SidecarStore.FileName), sidecar);
        LibraryReindexer.Rebuild(Path.Combine(workspace.WorkingDirectory, Library), includeContent: false);
    }

    [Then("the work {string} has the authors {string}")]
    public void ThenTheWorkHasTheAuthors(string title, string authors) =>
        Assert.Equal(authors, string.Join(", ", System.Text.Json.JsonSerializer.Deserialize<string[]>((string)Scalar("authors_json", title)!)!));

    [Then("the work {string} has no authors in the index")]
    public void ThenTheWorkHasNoAuthors(string title) =>
        Assert.Equal($"[\"{AuthorNames.UnknownAuthor}\"]", Scalar("authors_json", title));

    [Then("the work {string} has the year {int}")]
    public void ThenTheWorkHasTheYear(string title, int year) => Assert.Equal((long)year, Scalar("year", title));

    [Then("the work {string} has the cite key {string}")]
    public void ThenTheWorkHasTheCiteKey(string title, string key) => Assert.Equal(key, Scalar("cite_key", title));

    [Then("the work {string} has the venue {string}")]
    public void ThenTheWorkHasTheVenue(string title, string venue) => Assert.Equal(venue, Scalar("venue", title));

    private object? Scalar(string column, string title)
    {
        Assert.Matches("^[a-z_]+$", column);
        using var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(Path.Combine(workspace.WorkingDirectory, Library)));
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {column} FROM works WHERE title = $v";
        command.Parameters.AddWithValue("$v", title);
        var value = command.ExecuteScalar();
        return value is DBNull ? null : value;
    }
}
