using System.Diagnostics;
using Reqnroll;
using Xunit;

namespace Shelf.Specs.Steps;

/// <summary>
/// Steps for the completion scripts. The script of bash is run in the real shell and asked for completions; the scripts
/// of zsh and fish are checked for syntax by the shell itself when it is installed (a scenario is skipped when it is not).
/// </summary>
[Binding]
public sealed class CompletionSteps(ConfigSteps workspace, CliSteps cli)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private string _noted = string.Empty;
    private string[] _completions = [];

    [Given("I note the standard output")]
    public void GivenINoteTheStandardOutput() => _noted = cli.StandardOutput;

    [Then("standard output is the noted output")]
    public void ThenStandardOutputIsTheNotedOutput()
    {
        Assert.NotEmpty(_noted);
        Assert.Equal(_noted, cli.StandardOutput);
    }

    [Given("a folder {string}")]
    public void GivenAFolder(string name) => Directory.CreateDirectory(Path.Combine(workspace.WorkingDirectory, name));

    [When("I complete the line {string} in bash")]
    public void WhenICompleteTheLineInBash(string line)
    {
        var script = Path.Combine(workspace.WorkingDirectory, "completion.bash");
        File.WriteAllText(script, cli.StandardOutput);

        // What bash puts into COMP_WORDS: the words of the line, and an empty last word when the line ends with a space.
        var words = line.Split(' ');
        var quoted = string.Join(' ', words.Select(w => "'" + w + "'"));
        var commands = $"source completion.bash; COMP_WORDS=({quoted}); COMP_CWORD={words.Length - 1}; _shelf; printf '%s\\n' \"${{COMPREPLY[@]}}\"";
        var result = Run("bash", ["--norc", "--noprofile", "-c", commands]);
        Assert.True(result.Code == 0, $"bash failed: {result.Error}");
        _completions = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
    }

    [Then("the completions are {string}")]
    public void ThenTheCompletionsAre(string expected) =>
        Assert.Equal(expected.Split(' ', StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.Ordinal), _completions.Order(StringComparer.Ordinal));

    [Then("the completions include {string}")]
    public void ThenTheCompletionsInclude(string word) => Assert.Contains(word, _completions);

    [Then("the completions do not include {string}")]
    public void ThenTheCompletionsDoNotInclude(string word) => Assert.DoesNotContain(word, _completions);

    [Then("the script is valid for {word}")]
    public void ThenTheScriptIsValidFor(string shell)
    {
        var path = Path.Combine(workspace.WorkingDirectory, "completion." + shell);
        File.WriteAllText(path, cli.StandardOutput);
        string[] arguments = shell == "fish" ? ["--no-execute", path] : ["-n", path];
        var result = Run(shell, arguments);
        Assert.SkipWhen(result.Code == -1, $"{shell} is not installed");
        Assert.True(result.Code == 0, $"{shell} rejects the script: {result.Error}");
    }

    /// <summary>Runs a program; the code is -1 when it does not exist.</summary>
    private (int Code, string Output, string Error) Run(string program, string[] arguments)
    {
        var info = new ProcessStartInfo(program) { RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = workspace.WorkingDirectory };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        Process process;
        try
        {
            process = Process.Start(info)!;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return (-1, string.Empty, string.Empty);
        }

        using (process)
        {
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            Assert.True(process.WaitForExit(Timeout), $"{program} did not finish.");
            return (process.ExitCode, output.GetAwaiter().GetResult(), error.GetAwaiter().GetResult());
        }
    }
}
