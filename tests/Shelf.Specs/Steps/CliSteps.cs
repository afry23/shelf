using System.Text.RegularExpressions;
using Reqnroll;
using Shelf.Cli;
using Xunit;

namespace Shelf.Specs.Steps;

// Style convention for step definitions: Cucumber expressions ({string}, {int}).
[Binding]
public sealed class CliSteps
{
    private readonly StringWriter _stdout = new();
    private readonly StringWriter _stderr = new();
    private int _exitCode = -1;

    [When("I run shelf with arguments {string}")]
    public void WhenIRunShelfWithArguments(string arguments)
    {
        var args = arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        _exitCode = ShelfApp.Run(args, _stdout, _stderr);
    }

    [Then("the exit code is {int}")]
    public void ThenTheExitCodeIs(int expected) => Assert.Equal(expected, _exitCode);

    [Then("standard output matches the pattern {string}")]
    public void ThenStandardOutputMatchesThePattern(string pattern) =>
        Assert.Matches(new Regex(pattern), _stdout.ToString());

    [Then("standard output starts with {string}")]
    public void ThenStandardOutputStartsWith(string prefix) =>
        Assert.StartsWith(prefix, _stdout.ToString(), StringComparison.Ordinal);

    [Then("standard error starts with {string}")]
    public void ThenStandardErrorStartsWith(string prefix) =>
        Assert.StartsWith(prefix, _stderr.ToString(), StringComparison.Ordinal);

    [Then("standard output is empty")]
    public void ThenStandardOutputIsEmpty() => Assert.Equal(string.Empty, _stdout.ToString());

    [Then("standard error is empty")]
    public void ThenStandardErrorIsEmpty() => Assert.Equal(string.Empty, _stderr.ToString());
}
