using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Reqnroll;
using Shelf.Cli;
using Xunit;

namespace Shelf.Specs.Steps;

// Style convention for step definitions: Cucumber expressions ({string}, {int}).
[Binding]
public sealed class CliSteps(ScenarioEnvironment environment, ConfigSteps workspace)
{
    private readonly StringWriter _stdout = new();
    private readonly StringWriter _stderr = new();
    private bool _stdoutIsTerminal;
    private int _exitCode = -1;
    private LogLevel? _logLevel;

    [Given("the environment variable {string} is set to {string}")]
    public void GivenTheEnvironmentVariableIsSetTo(string name, string value) => environment.Variables[name] = value;

    [Given("standard output is a terminal")]
    public void GivenStandardOutputIsATerminal() => _stdoutIsTerminal = true;

    [Given("the log level is {string}")]
    public void GivenTheLogLevelIs(string value) => _logLevel = Enum.Parse<LogLevel>(value, ignoreCase: false);

    /// <summary>The home directory of scenarios that have a working directory: a folder inside it.</summary>
    public const string HomeFolder = ".home";

    [Given("I have run shelf with arguments {string}")]
    public void GivenIHaveRunShelfWithArguments(string arguments)
    {
        WhenIRunShelfWithArguments(arguments);
        Assert.Equal(0, _exitCode);
        _stdout.GetStringBuilder().Clear();
        _stderr.GetStringBuilder().Clear();
        _exitCode = -1;
    }

    [When("I run shelf with arguments {string}")]
    public void WhenIRunShelfWithArguments(string arguments)
    {
        var args = ArgumentSplitter.Split(arguments);
        var cliEnvironment = CliEnvironment.FromVariables(
            name => environment.Variables.GetValueOrDefault(name),
            _stdoutIsTerminal);
        if (workspace.HasWorkingDirectory)
        {
            cliEnvironment = cliEnvironment.WithDirectories(
                workspace.WorkingDirectory,
                Path.Combine(workspace.WorkingDirectory, HomeFolder));
        }

        if (_logLevel is { } level)
        {
            cliEnvironment = cliEnvironment.WithMinimumLogLevel(level);
        }

        _exitCode = ShelfApp.Run(args, _stdout, _stderr, cliEnvironment);
    }

    [Then("the exit code is {int}")]
    public void ThenTheExitCodeIs(int expected) => Assert.Equal(expected, _exitCode);

    [Then("standard output matches the pattern {string}")]
    public void ThenStandardOutputMatchesThePattern(string pattern) =>
        Assert.Matches(new Regex(pattern), _stdout.ToString());

    [Then("standard error starts with {string}")]
    public void ThenStandardErrorStartsWith(string prefix) =>
        Assert.StartsWith(prefix, _stderr.ToString(), StringComparison.Ordinal);

    [Then("standard output is empty")]
    public void ThenStandardOutputIsEmpty() => Assert.Equal(string.Empty, _stdout.ToString());

    [Then("standard error is empty")]
    public void ThenStandardErrorIsEmpty() => Assert.Equal(string.Empty, _stderr.ToString());

    [Then("standard output contains {string}")]
    public void ThenStandardOutputContains(string text) =>
        Assert.Contains(text, _stdout.ToString(), StringComparison.Ordinal);

    [Then("standard error contains {string}")]
    public void ThenStandardErrorContains(string text) =>
        Assert.Contains(text, _stderr.ToString(), StringComparison.Ordinal);

    [Then("standard output does not contain {string}")]
    public void ThenStandardOutputDoesNotContain(string text) =>
        Assert.DoesNotContain(text, _stdout.ToString(), StringComparison.Ordinal);

    [Then("standard error contains the line {string}")]
    public void ThenStandardErrorContainsTheLine(string line) =>
        Assert.Contains(line, StderrLines());

    [Then("standard error contains {int} log line(s) at level {string}")]
    public void ThenStandardErrorContainsLogLinesAtLevel(int count, string level)
    {
        var prefix = "[" + level + "] ";
        Assert.Equal(count, StderrLines().Count(l => l.StartsWith(prefix, StringComparison.Ordinal)));
    }

    private string[] StderrLines() =>
        _stderr.ToString().Split('\n').Select(l => l.TrimEnd('\r')).ToArray();

    [Then("standard error does not contain {string}")]
    public void ThenStandardErrorDoesNotContain(string text) =>
        Assert.DoesNotContain(text, _stderr.ToString(), StringComparison.Ordinal);

    [Then("standard output contains no ANSI escape sequences")]
    public void ThenStandardOutputContainsNoAnsiEscapeSequences() =>
        Assert.DoesNotContain('\u001b', _stdout.ToString());

    [Then("standard error contains no ANSI escape sequences")]
    public void ThenStandardErrorContainsNoAnsiEscapeSequences() =>
        Assert.DoesNotContain('\u001b', _stderr.ToString());

    [Then("standard output is a JSON object with the string property {string} matching {string}")]
    public void ThenStandardOutputIsAJsonObjectWithTheStringPropertyMatching(string property, string pattern)
    {
        using var document = JsonDocument.Parse(_stdout.ToString());
        var root = document.RootElement;
        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.Single(root.EnumerateObject());
        var value = root.GetProperty(property);
        Assert.Equal(JsonValueKind.String, value.ValueKind);
        Assert.Matches(new Regex(pattern), value.GetString()!);
    }

    [Then("standard output is not a JSON document")]
    public void ThenStandardOutputIsNotAJsonDocument()
    {
        var text = _stdout.ToString();
        try
        {
            using var document = JsonDocument.Parse(text);
        }
        catch (JsonException)
        {
            return;
        }

        Assert.Fail($"Standard output is valid JSON: {text}");
    }
}
