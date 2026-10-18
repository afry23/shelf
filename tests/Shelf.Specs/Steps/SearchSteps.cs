using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Reqnroll;
using Shelf.Core.Search;
using Shelf.TestData;
using Xunit;

namespace Shelf.Specs.Steps;

[Binding]
public sealed partial class SearchSteps(CliSteps cli, ConfigSteps workspace)
{
    private SearchQuery? _query;
    private QueryException? _error;

    [GeneratedRegex(@"^\d+\. (.+?) - ", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex ResultLine();

    // ---- sample books with known content ----

    [Given("a sample EPUB {string} with the title {string}, the authors {string} and the chapters:")]
    public void GivenASampleEpubWithChapters(string name, string title, string authors, DataTable chapters)
    {
        var path = Path.Combine(workspace.WorkingDirectory, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, SampleBooks.Epub(title, authors.Split("; "), [.. chapters.Rows.Select(r => (r["title"], r["text"]))]));
    }

    [Given("a sample PDF {string} with the title {string}, the author {string} and the pages:")]
    public void GivenASamplePdfWithPages(string name, string title, string author, DataTable pages)
    {
        var path = Path.Combine(workspace.WorkingDirectory, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, SampleBooks.Pdf(title, author, [.. pages.Rows.Select(r => r["text"])]));
    }

    // ---- the query language ----

    [When("shelf parses the search query {string}")]
    public void WhenShelfParsesTheSearchQuery(string text)
    {
        try
        {
            _query = QueryParser.Parse(text);
        }
        catch (QueryException e)
        {
            _error = e;
        }
    }

    [Then("the parsed query is {string}")]
    public void ThenTheParsedQueryIs(string expected)
    {
        Assert.Null(_error);
        Assert.Equal(expected, _query!.Describe());
    }

    [Then("parsing the search query fails with {string}")]
    public void ThenParsingFailsWith(string message)
    {
        Assert.NotNull(_error);
        Assert.Equal(message, _error.Message);
    }

    // ---- results of the command ----

    [Then("the search results are in this order: {string}")]
    public void ThenTheSearchResultsAreInThisOrder(string titles) => Assert.Equal(Split(titles), Titles());

    [Then("the search results are, in any order: {string}")]
    public void ThenTheSearchResultsAreInAnyOrder(string titles) =>
        Assert.Equal(Split(titles).Order(StringComparer.Ordinal), Titles().Order(StringComparer.Ordinal));

    [Then("the search results have {int} entries")]
    public void ThenTheSearchResultsHaveEntries(int count) => Assert.Equal(count, Titles().Count);

    [Then("the JSON of standard output has the string {string} equal to {string}")]
    public void ThenTheJsonHasTheString(string path, string expected) => Assert.Equal(expected, Navigate(path).GetString());

    [Then("the JSON of standard output has the string {string} containing {string}")]
    public void ThenTheJsonHasTheStringContaining(string path, string text) =>
        Assert.Contains(text, Navigate(path).GetString()!, StringComparison.Ordinal);

    [Then("the JSON of standard output has no property {string}")]
    public void ThenTheJsonHasNoProperty(string name) =>
        Assert.False(JsonDocument.Parse(cli.StandardOutput).RootElement.TryGetProperty(name, out _), $"Unexpected property {name}");

    [Then("the JSON of standard output has the decimal {string} equal to {double}")]
    public void ThenTheJsonHasTheDecimal(string path, double expected) => Assert.Equal(expected, Navigate(path).GetDouble(), 3);

    [Then("the JSON of standard output has the number {string} equal to {int}")]
    public void ThenTheJsonHasTheNumber(string path, int expected) => Assert.Equal(expected, Navigate(path).GetInt32());

    private List<string> Titles() =>
        [.. ResultLine().Matches(cli.StandardOutput).Select(m => m.Groups[1].Value)];

    private static List<string> Split(string titles) => titles.Length == 0 ? [] : [.. titles.Split("; ")];

    /// <summary>A path like <c>results[0].hits[1].file</c> into the JSON that the command wrote.</summary>
    private JsonElement Navigate(string path)
    {
        var element = JsonDocument.Parse(cli.StandardOutput).RootElement.Clone();
        foreach (var part in path.Split('.'))
        {
            var bracket = part.IndexOf('[', StringComparison.Ordinal);
            var name = bracket < 0 ? part : part[..bracket];
            element = element.GetProperty(name);
            if (bracket >= 0)
            {
                element = element[int.Parse(part[(bracket + 1)..^1], CultureInfo.InvariantCulture)];
            }
        }

        return element;
    }
}
