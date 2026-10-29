using System.Globalization;
using Reqnroll;
using Shelf.Index;
using Xunit;

namespace Shelf.Specs.Steps;

/// <summary>Steps for <c>shelf enrich</c>: online catalogs that answer from a table instead of the internet.</summary>
[Binding]
public sealed class EnrichSteps(ScenarioEnvironment environment)
{
    [Given("the online source {string} holds the book:")]
    public void GivenTheSourceHoldsTheBook(string name, DataTable table)
    {
        var fields = table.Rows.ToDictionary(r => r["field"], r => r["value"], StringComparer.Ordinal);
        string? Field(string key) => fields.GetValueOrDefault(key);
        environment.Online.Source(name).Books.Add(new OnlineBook(
            name,
            Field("isbn13"),
            Field("title")!,
            Field("authors") is { } authors ? [.. authors.Split(", ")] : [],
            Field("subtitle"),
            Field("publisher"),
            Field("year") is { } year ? int.Parse(year, CultureInfo.InvariantCulture) : null,
            Field("language"),
            Field("description"),
            "https://example.invalid/cover"));
    }

    [Given("the online source {string} holds the paper:")]
    public void GivenTheSourceHoldsThePaper(string name, DataTable table)
    {
        var fields = table.Rows.ToDictionary(r => r["field"], r => r["value"], StringComparer.Ordinal);
        string? Field(string key) => fields.GetValueOrDefault(key);
        environment.Online.Source(name).Books.Add(new OnlineBook(
            name,
            null,
            Field("title")!,
            Field("authors") is { } authors ? [.. authors.Split(", ")] : [],
            Year: Field("year") is { } year ? int.Parse(year, CultureInfo.InvariantCulture) : null,
            Publisher: Field("publisher"),
            Doi: Field("doi"),
            ArxivId: Field("arxiv"),
            Venue: Field("venue"),
            Abstract: Field("abstract")));
    }

    [Given("the online source {string} lists these references for the DOI {string}:")]
    public void GivenTheSourceListsReferences(string name, string doi, DataTable table)
    {
        string? Cell(DataTableRow row, string column) => table.Header.Contains(column) && !string.IsNullOrEmpty(row[column]) ? row[column] : null;
        environment.Online.Source(name).References[doi] =
        [
            .. table.Rows.Select(r => new Shelf.Core.Model.Reference(
                Cell(r, "doi"),
                Cell(r, "title"),
                Cell(r, "year") is { } year ? int.Parse(year, CultureInfo.InvariantCulture) : null,
                Cell(r, "author"),
                Cell(r, "unstructured"))),
        ];
    }

    [Given("the online source {string} has a PDF for the arXiv ID {string}")]
    public void GivenTheSourceHasAPdf(string name, string arxivId) =>
        environment.Online.Source(name).Pdfs[arxivId] = Shelf.TestData.SampleBooks.PaperPdf("Attention Is All You Need", "Ashish Vaswani", "arXiv:" + arxivId + "v5 [cs.CL]");

    [Given("the online source {string} has a PDF for the DOI {string}")]
    public void GivenTheSourceHasAPdfForTheDoi(string name, string doi) =>
        environment.Online.Source(name).DoiPdfs[doi] = Shelf.TestData.SampleBooks.PaperPdf("Deep Residual Learning for Image Recognition", "Kaiming He", "doi:" + doi);

    [Then("the online source {string} was queried")]
    public void ThenTheSourceWasQueried(string name) =>
        Assert.Contains(environment.Online.Calls, c => c.StartsWith(name + ":", StringComparison.Ordinal));

    [Given("the online source {string} holds no books")]
    public void GivenTheSourceHoldsNoBooks(string name) => environment.Online.Source(name).Books.Clear();

    [Given("the online source {string} fails with {string}")]
    public void GivenTheSourceFails(string name, string message) => environment.Online.Source(name).FailWith = message;

    [Given("the online source {string} has a cover of type {string} for the ISBN {string}")]
    public void GivenTheSourceHasACover(string name, string type, string isbn) =>
        environment.Online.Source(name).Covers[isbn] = new OnlineCover([0xFF, 0xD8, 0xFF, 0xE0], type);

    [Given("the online questions are forgotten")]
    public void GivenTheOnlineQuestionsAreForgotten() => environment.Online.Calls.Clear();

    [Then("no online source was queried")]
    public void ThenNoSourceWasQueried() => Assert.Empty(environment.Online.Calls);

    [Then("the online source {string} was not queried")]
    public void ThenTheSourceWasNotQueried(string name) =>
        Assert.DoesNotContain(environment.Online.Calls, c => c.StartsWith(name + ":", StringComparison.Ordinal));

    [Then("the online source {string} was queried before the online source {string}")]
    public void ThenTheSourceWasQueriedBefore(string first, string second)
    {
        var calls = environment.Online.Calls;
        var a = calls.FindIndex(c => c.StartsWith(first + ":", StringComparison.Ordinal));
        var b = calls.FindIndex(c => c.StartsWith(second + ":", StringComparison.Ordinal));
        Assert.True(a >= 0 && b > a, $"calls: {string.Join(", ", calls)}");
    }
}

/// <summary>Both catalogs of <c>enrich</c> and the log of the questions they were asked.</summary>
public sealed class FakeOnline
{
    private readonly List<FakeSource> _sources;

    public FakeOnline() => _sources =
    [
        new FakeSource("openlibrary", this),
        new FakeSource("google", this),
        new FakeSource("crossref", this, forPapers: true),
        new FakeSource("arxiv", this, forPapers: true),
        new FakeSource("unpaywall", this, forPapers: true, needsEmail: true),
    ];

    public IReadOnlyList<IMetadataSource> Sources => _sources;

    /// <summary>One entry per question, <c>openlibrary:isbn 978…</c> or <c>google:search Title</c>, in the order asked.</summary>
    public List<string> Calls { get; } = [];

    public FakeSource Source(string name) => _sources.Single(s => s.Name == name);
}

public sealed class FakeSource(string name, FakeOnline online, bool forPapers = false, bool needsEmail = false) : IMetadataSource
{
    /// <summary>Like Unpaywall: without an e-mail address in the settings the source is left out.</summary>
    public IMetadataSource? ForSettings(Shelf.Core.Configuration.FetchSettings settings) =>
        needsEmail && settings.UnpaywallEmail is null ? null : this;

    /// <summary>PDFs by DOI, in lower case.</summary>
    public Dictionary<string, byte[]> DoiPdfs { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string Name => name;

    public bool ForPapers => forPapers;

    public IReadOnlyList<OnlineBook> FindByDoi(string doi, CancellationToken cancellation)
    {
        online.Calls.Add($"{name}:doi {doi}");
        Fail();
        return [.. Books.Where(b => string.Equals(b.Doi, doi, StringComparison.OrdinalIgnoreCase))];
    }

    /// <summary>The reference lists by DOI (lower case); a DOI that is not here is unknown to the source.</summary>
    public Dictionary<string, IReadOnlyList<Shelf.Core.Model.Reference>> References { get; } = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<Shelf.Core.Model.Reference>? FindReferences(string doi, CancellationToken cancellation)
    {
        online.Calls.Add($"{name}:references {doi}");
        Fail();
        return References.GetValueOrDefault(doi);
    }

    public IReadOnlyList<OnlineBook> FindByArxivId(string arxivId, CancellationToken cancellation)
    {
        online.Calls.Add($"{name}:arxiv {arxivId}");
        Fail();
        return [.. Books.Where(b => b.ArxivId == arxivId)];
    }

    public List<OnlineBook> Books { get; } = [];

    public Dictionary<string, OnlineCover> Covers { get; } = new();

    /// <summary>PDFs by arXiv ID.</summary>
    public Dictionary<string, byte[]> Pdfs { get; } = new();

    public byte[]? FetchPdf(OnlineBook paper, CancellationToken cancellation)
    {
        online.Calls.Add($"{name}:pdf {paper.ArxivId ?? paper.Doi}");
        Fail();
        if (paper.ArxivId is { } id && Pdfs.TryGetValue(id, out var byArxiv))
        {
            return byArxiv;
        }

        return paper.Doi is { } doi ? DoiPdfs.GetValueOrDefault(doi) : null;
    }

    public string? FailWith { get; set; }

    public IReadOnlyList<OnlineBook> FindByIsbn(string isbn13, CancellationToken cancellation)
    {
        online.Calls.Add($"{name}:isbn {isbn13}");
        Fail();
        return [.. Books.Where(b => b.Isbn13 == isbn13)];
    }

    /// <summary>Answers with every book it holds; telling a good match from a bad one is the job of shelf.</summary>
    public IReadOnlyList<OnlineBook> Search(string title, string? author, CancellationToken cancellation)
    {
        online.Calls.Add($"{name}:search {title}");
        Fail();
        return [.. Books];
    }

    public OnlineCover? FetchCover(OnlineBook book, CancellationToken cancellation)
    {
        online.Calls.Add($"{name}:cover {book.Isbn13}");
        Fail();
        return Covers.GetValueOrDefault(book.Isbn13 ?? string.Empty);
    }

    private void Fail()
    {
        if (FailWith is not null)
        {
            throw new MetadataSourceException(FailWith);
        }
    }
}
