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
    private string _standardInput = string.Empty;
    private int _exitCode = -1;
    private LogLevel? _logLevel;
    private string? _folder;
    private readonly RecordingFileOpener _opener = new();

    [Given("the environment variable {string} is set to {string}")]
    public void GivenTheEnvironmentVariableIsSetTo(string name, string value) => environment.Variables[name] = value;

    [Given("standard output is a terminal")]
    public void GivenStandardOutputIsATerminal() => _stdoutIsTerminal = true;

    [Given("the log level is {string}")]
    public void GivenTheLogLevelIs(string value) => _logLevel = Enum.Parse<LogLevel>(value, ignoreCase: false);

    /// <summary>The home directory of scenarios that have a working directory: a folder inside it.</summary>
    public const string HomeFolder = ".home";

    [Given("I am in the folder {string}")]
    public void GivenIAmInTheFolder(string folder) => _folder = folder;

    /// <summary>The level of "the log level is", or null when no scenario step set one.</summary>
    public LogLevel? MinimumLogLevel => _logLevel;

    [Given("I have run shelf with arguments {string}")]
    public void GivenIHaveRunShelfWithArguments(string arguments)
    {
        WhenIRunShelfWithArguments(arguments);
        Assert.Equal(0, _exitCode);
        _stdout.GetStringBuilder().Clear();
        _stderr.GetStringBuilder().Clear();
        _exitCode = -1;
    }

    [Given("I have tried shelf with arguments {string}")]
    public void GivenIHaveTriedShelfWithArguments(string arguments)
    {
        WhenIRunShelfWithArguments(arguments);
        _stdout.GetStringBuilder().Clear();
        _stderr.GetStringBuilder().Clear();
        _exitCode = -1;
    }

    /// <summary>What the last run wrote to standard output.</summary>
    public string StandardOutput => _stdout.ToString();

    [Given("I run shelf with arguments {string}")]
    [When("I run shelf with arguments {string}")]
    public void WhenIRunShelfWithArguments(string arguments)
    {
        // Each run starts with empty output: a scenario can run several commands and look at the last one.
        _stdout.GetStringBuilder().Clear();
        _stderr.GetStringBuilder().Clear();
        var args = ArgumentSplitter.Split(arguments);
        var cliEnvironment = CliEnvironment.FromVariables(
            name => environment.Variables.GetValueOrDefault(name),
            _stdoutIsTerminal);
        if (workspace.HasWorkingDirectory)
        {
            cliEnvironment = cliEnvironment.WithDirectories(
                _folder is null ? workspace.WorkingDirectory : Path.Combine(workspace.WorkingDirectory, _folder),
                Path.Combine(workspace.WorkingDirectory, HomeFolder));
        }

        if (_logLevel is { } level)
        {
            cliEnvironment = cliEnvironment.WithMinimumLogLevel(level);
        }

        cliEnvironment = cliEnvironment.WithStandardInput(new StringReader(_standardInput));

        if (environment.Models is { } models)
        {
            cliEnvironment = cliEnvironment.WithModels(models.Catalog, models);
        }

        if (environment.Embedder is { } embedder)
        {
            cliEnvironment = cliEnvironment.WithEmbedder(folder =>
            {
                environment.EmbedderOpens++;
                return environment.EmbedderFor(Path.GetFileName(folder.Directory));
            });
        }

        cliEnvironment = cliEnvironment.WithCancellation(environment.Interrupt.Token);

        // Scenarios never start a real application.
        cliEnvironment = cliEnvironment.WithFileOpener(_opener);

        _exitCode = ShelfApp.Run(args, _stdout, _stderr, cliEnvironment);
    }

    /// <summary>The lines that the next runs read from standard input; "&lt;enter&gt;" is an empty line.</summary>
    [Given("the answers are {string}")]
    public void GivenTheAnswersAre(string answers) =>
        _standardInput = string.Join('\n', answers.Split(", ").Select(a => a == "<enter>" ? string.Empty : a)) + "\n";

    [Given("the system fails to open files with the message {string}")]
    public void GivenTheSystemFailsToOpenFiles(string message) => _opener.FailWith = message;

    [Then("shelf asked the system to open {string}")]
    public void ThenShelfAskedTheSystemToOpen(string path) =>
        Assert.Equal([Path.GetFullPath(Path.Combine(workspace.WorkingDirectory, path))], _opener.Opened);

    [Then("shelf asked the system to open nothing")]
    public void ThenShelfAskedTheSystemToOpenNothing() => Assert.Empty(_opener.Opened);

    [Then("standard output contains an ANSI escape sequence")]
    public void ThenStandardOutputContainsAnAnsiEscapeSequence() =>
        Assert.Contains('\u001b', _stdout.ToString());

    [Then("standard output without ANSI escape sequences has the line {string}")]
    public void ThenStandardOutputWithoutAnsiHasTheLine(string line) =>
        Assert.Contains(line, Regex.Replace(_stdout.ToString(), "\u001b\\[[0-9;]*m", string.Empty).Split('\n').Select(l => l.TrimEnd('\r')));

    [Then("the exit code is {int}")]
    public void ThenTheExitCodeIs(int expected)
    {
        if (expected != _exitCode)
        {
            Assert.Fail($"Expected exit code {expected} but it was {_exitCode}. Standard error: {_stderr}Standard output: {_stdout}");
        }
    }

    [Then("standard output matches the pattern {string}")]
    public void ThenStandardOutputMatchesThePattern(string pattern)
    {
        if (!new Regex(pattern).IsMatch(_stdout.ToString()))
        {
            Assert.Fail($"Standard output does not match the pattern {pattern}. Standard output was:{Environment.NewLine}{_stdout}");
        }
    }

    [Then("standard error starts with {string}")]
    public void ThenStandardErrorStartsWith(string prefix) =>
        Assert.StartsWith(prefix, _stderr.ToString(), StringComparison.Ordinal);

    [Then("standard output is {string}")]
    public void ThenStandardOutputIs(string expected) => Assert.Equal(expected, _stdout.ToString().TrimEnd('\r', '\n'));

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

    [Then("the JSON integer property {string} of standard output is {int}")]
    public void ThenTheJsonIntegerPropertyOfStandardOutputIs(string property, int expected)
    {
        using var document = JsonDocument.Parse(_stdout.ToString());
        Assert.Equal(expected, document.RootElement.GetProperty(property).GetInt32());
    }

    [Then("the JSON string property {string} of standard output matches {string}")]
    public void ThenTheJsonStringPropertyOfStandardOutputMatches(string property, string pattern)
    {
        using var document = JsonDocument.Parse(_stdout.ToString());
        var value = document.RootElement.GetProperty(property);
        Assert.Equal(JsonValueKind.String, value.ValueKind);
        Assert.Matches(new Regex(pattern), value.GetString()!);
    }

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

    [Then("standard output is a JSON object with the integer properties {string}")]
    public void ThenStandardOutputIsAJsonObjectWithTheIntegerProperties(string expected)
    {
        using var document = JsonDocument.Parse(_stdout.ToString());
        var root = document.RootElement;
        var pairs = expected.Split(", ").Select(p => p.Split('=')).ToList();
        foreach (var pair in pairs)
        {
            Assert.Equal(int.Parse(pair[1], System.Globalization.CultureInfo.InvariantCulture), root.GetProperty(pair[0]).GetInt32());
        }
    }

    [Then("the JSON property {string} of standard output has {int} items")]
    public void ThenTheJsonPropertyHasItems(string property, int count)
    {
        using var document = JsonDocument.Parse(_stdout.ToString());
        Assert.Equal(count, document.RootElement.GetProperty(property).GetArrayLength());
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

/// <summary>Stands in for the system's file opener: remembers what it was asked to open.</summary>
internal sealed class RecordingFileOpener : IFileOpener
{
    public List<string> Opened { get; } = [];

    public string? FailWith { get; set; }

    public void Open(string path)
    {
        if (FailWith is { } message)
        {
            throw new IOException(message);
        }

        Opened.Add(path);
    }
}
