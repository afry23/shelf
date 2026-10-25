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

    [Given("the online source {string} holds no books")]
    public void GivenTheSourceHoldsNoBooks(string name) => environment.Online.Source(name).Books.Clear();

    [Given("the online source {string} fails with {string}")]
    public void GivenTheSourceFails(string name, string message) => environment.Online.Source(name).FailWith = message;

    [Given("the online source {string} has a cover of type {string} for the ISBN {string}")]
    public void GivenTheSourceHasACover(string name, string type, string isbn) =>
        environment.Online.Source(name).Covers[isbn] = new OnlineCover([0xFF, 0xD8, 0xFF, 0xE0], type);

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

    public FakeOnline() => _sources = [new FakeSource("openlibrary", this), new FakeSource("google", this)];

    public IReadOnlyList<IMetadataSource> Sources => _sources;

    /// <summary>One entry per question, <c>openlibrary:isbn 978…</c> or <c>google:search Title</c>, in the order asked.</summary>
    public List<string> Calls { get; } = [];

    public FakeSource Source(string name) => _sources.Single(s => s.Name == name);
}

public sealed class FakeSource(string name, FakeOnline online) : IMetadataSource
{
    public string Name => name;

    public List<OnlineBook> Books { get; } = [];

    public Dictionary<string, OnlineCover> Covers { get; } = new();

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
