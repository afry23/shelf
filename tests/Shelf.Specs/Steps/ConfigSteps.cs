using System.Globalization;
using System.Text;
using Reqnroll;
using Shelf.Core.Configuration;
using Xunit;

namespace Shelf.Specs.Steps;

// Style convention for step definitions: Cucumber expressions ({string}, {int}, {word}).
[Binding]
public sealed class ConfigSteps
{
    private string? _workingDirectory;

    /// <summary>The scratch folder of the scenario ("an empty working directory"); shared with other step classes.</summary>
    public bool HasWorkingDirectory => _workingDirectory is not null;

    public string WorkingDirectory => _workingDirectory ?? throw new InvalidOperationException("The scenario has no working directory.");
    private ConfigDocument? _document;
    private ConfigException? _loadError;
    private ConfigException? _readError;

    [Given("an empty working directory")]
    public void GivenAnEmptyWorkingDirectory()
    {
        _workingDirectory = Path.Combine(Path.GetTempPath(), "shelf-specs", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workingDirectory);
    }

    [Given("a config file {string} with content:")]
    public void GivenAConfigFileWithContent(string name, string content)
    {
        var text = content.Replace("\r\n", "\n", StringComparison.Ordinal);
        File.WriteAllText(PathOf(name), text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    [When("shelf loads the config file {string}")]
    public void WhenShelfLoadsTheConfigFile(string name)
    {
        try
        {
            _document = ConfigFile.Load(PathOf(name));
        }
        catch (ConfigException e)
        {
            _loadError = e;
        }
    }

    [When("shelf reads the {word} value at {string}")]
    public void WhenShelfReadsTheValueAt(string type, string key)
    {
        try
        {
            var document = Document();
            switch (type)
            {
                case "string":
                    document.GetString(key);
                    break;
                case "integer":
                    document.GetInteger(key);
                    break;
                case "float":
                    document.GetFloat(key);
                    break;
                case "boolean":
                    document.GetBoolean(key);
                    break;
                default:
                    throw new ArgumentException($"Unknown value type '{type}'.", nameof(type));
            }
        }
        catch (ConfigException e)
        {
            _readError = e;
        }
    }

    [Then("loading succeeds")]
    public void ThenLoadingSucceeds()
    {
        Assert.Null(_loadError);
        Assert.NotNull(_document);
    }

    [Then("loading fails with an error at line {int}")]
    public void ThenLoadingFailsWithAnErrorAtLine(int line)
    {
        Assert.NotNull(_loadError);
        Assert.Equal(line, _loadError.Line);
    }

    [Then("the error message contains {string}")]
    public void ThenTheErrorMessageContains(string text)
    {
        Assert.NotNull(_loadError);
        Assert.Contains(text, _loadError.Message, StringComparison.Ordinal);
    }

    [Then("reading fails with an error containing {string}")]
    public void ThenReadingFailsWithAnErrorContaining(string text)
    {
        Assert.Null(_loadError);
        Assert.NotNull(_readError);
        Assert.Contains(text, _readError.Message, StringComparison.Ordinal);
    }

    [Then("the {word} value at {string} is {string}")]
    public void ThenTheValueAtIs(string type, string key, string expected)
    {
        var document = Document();
        switch (type)
        {
            case "string":
                Assert.Equal(expected, document.GetString(key));
                break;
            case "integer":
                Assert.Equal(long.Parse(expected, CultureInfo.InvariantCulture), document.GetInteger(key));
                break;
            case "float":
                Assert.Equal(double.Parse(expected, CultureInfo.InvariantCulture), document.GetFloat(key));
                break;
            case "boolean":
                Assert.Equal(bool.Parse(expected), document.GetBoolean(key));
                break;
            default:
                throw new ArgumentException($"Unknown value type '{type}'.", nameof(type));
        }
    }

    [Then("there is no value at {string}")]
    public void ThenThereIsNoValueAt(string key) => Assert.False(Document().Contains(key));

    [AfterScenario]
    public void DeleteWorkingDirectory()
    {
        if (_workingDirectory is not null && Directory.Exists(_workingDirectory))
        {
            Directory.Delete(_workingDirectory, recursive: true);
        }
    }

    private ConfigDocument Document()
    {
        Assert.Null(_loadError);
        Assert.NotNull(_document);
        return _document;
    }

    private string PathOf(string name)
    {
        Assert.NotNull(_workingDirectory);
        return Path.Combine(_workingDirectory, name);
    }
}
