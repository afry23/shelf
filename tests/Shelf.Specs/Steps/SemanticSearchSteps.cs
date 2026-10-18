using System.Text.Json;
using System.Text.RegularExpressions;
using Reqnroll;
using Xunit;

namespace Shelf.Specs.Steps;

/// <summary>Steps for <c>shelf semantic</c>: the order of results and the scores in the JSON output.</summary>
[Binding]
public sealed partial class SemanticSearchSteps(CliSteps cli, ScenarioEnvironment environment)
{
    [GeneratedRegex(@"^\d+\. (.+?) - ", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex ResultLine();

    [Then("the first search result is {string}")]
    public void ThenTheFirstSearchResultIs(string title) =>
        Assert.Equal(title, ResultLine().Match(cli.StandardOutput).Groups[1].Value);

    [Then("the JSON score {string} is greater than the JSON score {string}")]
    public void ThenTheScoreIsGreaterThan(string higher, string lower) =>
        Assert.True(Score(higher) > Score(lower), $"{higher} = {Score(higher)} should be greater than {lower} = {Score(lower)}");

    [Then("the JSON score {string} is at least {double}")]
    public void ThenTheScoreIsAtLeast(string path, double minimum) => Assert.True(Score(path) >= minimum, $"{path} = {Score(path)}");

    [Then("the embedder embedded exactly one query {string}")]
    public void ThenTheEmbedderEmbeddedExactlyOneQuery(string text) =>
        Assert.Equal([text], environment.Embedder!.Calls.Where(c => c.Kind == "query").Select(c => c.Text));

    private double Score(string path)
    {
        using var document = JsonDocument.Parse(cli.StandardOutput);
        var match = Regex.Match(path, @"^(\w+)\[(\d+)\]\.(\w+)$");
        return document.RootElement.GetProperty(match.Groups[1].Value)[int.Parse(match.Groups[2].Value)].GetProperty(match.Groups[3].Value).GetDouble();
    }
}
