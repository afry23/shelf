using System.IO.Compression;
using System.Text.RegularExpressions;
using Reqnroll;
using Shelf.Index;
using Shelf.TestData;
using Xunit;

namespace Shelf.Specs.Steps;

/// <summary>Steps of <c>shelf convert</c> (F-CNV-*): a fake converter that writes small EPUB files instead of starting a tool.</summary>
[Binding]
public sealed class ConvertSteps(ConfigSteps workspace, ScenarioEnvironment environment)
{
    private const string Library = "library";
    private const string RealConverterVariable = "SHELF_REAL_CONVERTER";

    private string _text = string.Empty;

    [Given("the converter is configured as {string}")]
    public void GivenTheConverterIsConfiguredAs(string command) => AppendConfig($"convert.command = '{command}'");

    [Given("the configuration key {string} is {string}")]
    public void GivenTheConfigurationKeyIs(string key, string value) => AppendConfig($"{key} = {value}");

    [Given("the converter writes an EPUB with the chapter text {string}")]
    public void GivenTheConverterWritesAnEpub(string text) => environment.Converter.Writes = _ => SampleBooks.Epub("Converted", ["Test Author"], [("Chapter 1", text)]);

    [Given("the converter writes no output file")]
    public void GivenTheConverterWritesNoOutputFile() => environment.Converter.Writes = null;

    [Given("the converter writes a file that is not a zip")]
    public void GivenTheConverterWritesAFileThatIsNotAZip() => environment.Converter.Writes = _ => "This is not an EPUB."u8.ToArray();

    [Given("the converter writes an EPUB without any chapter text")]
    public void GivenTheConverterWritesAnEpubWithoutText() => environment.Converter.Writes = _ => SampleBooks.Epub("Converted", ["Test Author"], [(string.Empty, string.Empty)]);

    [Given("the converter is not installed")]
    public void GivenTheConverterIsNotInstalled() => environment.Converter.Missing = true;

    [Given("the converter fails with {string}")]
    public void GivenTheConverterFailsWith(string message) => environment.Converter.FailWith = message;

    [Given("the converter runs for {int} seconds")]
    public void GivenTheConverterRunsFor(int seconds) => environment.Converter.RunSeconds = seconds;

    [Given("the fixture {string} is the only file of a work with the ID {string}")]
    public void GivenTheFixtureIsTheOnlyFileOfAWork(string fixture, string id)
    {
        var library = Path.Combine(workspace.WorkingDirectory, Library);
        var name = Path.GetFileName(fixture);
        var folder = Path.Combine(library, "Test Author", Path.GetFileNameWithoutExtension(name));
        Directory.CreateDirectory(folder);
        File.Copy(CoverSteps.FixturePath(fixture), Path.Combine(folder, name));
        File.WriteAllText(
            Path.Combine(folder, "book.json"),
            $$"""{ "schema": 1, "id": "{{id}}", "title": "{{Path.GetFileNameWithoutExtension(name)}}", "authors": ["Test Author"], "language": "en", "files": [{ "name": "{{name}}" }] }""");
        LibraryReindexer.Rebuild(library, includeContent: false);
    }

    [Given("the real converter of the configuration is installed")]
    public void GivenTheRealConverterIsInstalled()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable(RealConverterVariable) == "1", $"set {RealConverterVariable}=1 and install marker and pandoc (tools/pdf2epub.sh)");
        environment.Converter.Real = true;
        AppendConfig($"convert.command = '{Path.Combine(RepositoryPaths.Root(), "tools", "pdf2epub.sh")} {{input}} {{output}}'");
        AppendConfig("convert.timeout = 1800");
    }

    [When("I read the text of the new EPUB")]
    public void WhenIReadTheTextOfTheNewEpub()
    {
        var epub = Directory.EnumerateFiles(Path.Combine(workspace.WorkingDirectory, Library), "*.epub", SearchOption.AllDirectories).Single();
        using var archive = ZipFile.OpenRead(epub);
        var chapters = archive.Entries.Where(e => e.FullName.StartsWith("EPUB/text/", StringComparison.Ordinal) && e.Name.StartsWith("ch", StringComparison.Ordinal) && e.Name.EndsWith(".xhtml", StringComparison.Ordinal));
        _text = string.Join('\n', chapters.OrderBy(e => e.FullName, StringComparer.Ordinal).Select(e =>
        {
            using var reader = new StreamReader(e.Open());
            return Regex.Replace(reader.ReadToEnd(), "<[^>]*>", " ");
        }));
        _text = Regex.Replace(_text, @"\s+", " ");
    }

    [Then("the phrase {string} comes before the phrase {string}")]
    public void ThenThePhraseComesBefore(string first, string second)
    {
        var a = _text.IndexOf(first, StringComparison.Ordinal);
        var b = _text.IndexOf(second, StringComparison.Ordinal);
        Assert.True(a >= 0, $"'{first}' not found in: {_text}");
        Assert.True(b >= 0, $"'{second}' not found in: {_text}");
        Assert.True(a < b, $"'{first}' ({a}) must come before '{second}' ({b}) in: {_text}");
    }

    [Then("the converter was called with an input that ends in {string}")]
    public void ThenTheConverterWasCalledWithAnInput(string suffix) =>
        Assert.Contains(environment.Converter.Calls, call => call.Input.EndsWith(suffix, StringComparison.Ordinal));

    [Then("the converter was called with an output that ends in {string}")]
    public void ThenTheConverterWasCalledWithAnOutput(string suffix) =>
        Assert.Contains(environment.Converter.Calls, call => call.Output.EndsWith(suffix, StringComparison.Ordinal));

    [Then("the converter was not called")]
    public void ThenTheConverterWasNotCalled() => Assert.Empty(environment.Converter.Calls);

    [Then("no conversion folder is left behind")]
    public void ThenNoConversionFolderIsLeftBehind()
    {
        Assert.NotEmpty(environment.Converter.Calls);
        Assert.All(environment.Converter.Calls, call => Assert.False(Directory.Exists(Path.GetDirectoryName(call.Output)), call.Output));
    }

    private void AppendConfig(string line)
    {
        var path = Path.Combine(workspace.WorkingDirectory, Library, ".shelf", "config.toml");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.AppendAllText(path, line + "\n");
    }
}

/// <summary>One call of the converter: the paths it got.</summary>
public sealed record ConverterCall(string Input, string Output);

/// <summary>Writes what a scenario says instead of starting a tool, and records every call.</summary>
public sealed class FakeConverter : IPdfConverter
{
    /// <summary>The content of the output file for a call; null writes none.</summary>
    public Func<ConverterCall, byte[]>? Writes { get; set; }

    public bool Missing { get; set; }

    /// <summary>Run the user's real converter instead of writing a fake result (<c>ConvertColumns.feature</c>).</summary>
    public bool Real { get; set; }

    public string? FailWith { get; set; }

    /// <summary>How long the tool would run; longer than the time limit ends in a timeout without waiting.</summary>
    public int? RunSeconds { get; set; }

    public List<ConverterCall> Calls { get; } = [];

    public void Convert(string command, string input, string output, TimeSpan timeout, CancellationToken cancellation)
    {
        if (Real)
        {
            new ProcessConverter().Convert(command, input, output, timeout, cancellation);
            return;
        }

        var call = new ConverterCall(input, output);
        var tool = ProcessConverter.Arguments(command, input, output)[0];
        if (Missing)
        {
            throw new ConvertException($"{tool} was not found; install it or change convert.command", ConvertFailureKind.Usage);
        }

        Calls.Add(call);
        if (FailWith is not null)
        {
            throw new ConvertException($"{tool} failed (exit code 1): {FailWith}", ConvertFailureKind.Failed);
        }

        if (RunSeconds is { } seconds && seconds > timeout.TotalSeconds)
        {
            throw new ConvertException($"{tool} timed out after {(int)timeout.TotalSeconds} s", ConvertFailureKind.Failed);
        }

        if (Writes?.Invoke(call) is { } bytes)
        {
            File.WriteAllBytes(output, bytes);
        }
    }
}
