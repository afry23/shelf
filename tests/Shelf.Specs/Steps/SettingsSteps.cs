using System.Globalization;
using System.Text;
using Reqnroll;
using Shelf.Core.Configuration;
using Shelf.Core.Libraries;
using Xunit;

namespace Shelf.Specs.Steps;

/// <summary>
/// Steps for the layered settings and the library lookup. The config texts are parsed in memory
/// ("global.toml" and "library.toml" are the source names in messages); directories are a set of paths.
/// </summary>
[Binding]
public sealed class SettingsSteps
{
    private const string GlobalSource = "global.toml";
    private const string LibrarySource = "library.toml";

    private readonly StringBuilder _globalConfig = new();
    private readonly StringBuilder _libraryConfig = new();
    private readonly HashSet<string> _directories = [];
    private string _workingDirectory = "/";
    private ShelfSettings? _settings;
    private ConfigException? _settingsError;
    private LibraryResolution? _resolution;
    private LibraryNotFoundException? _locateError;

    [Given("the global config is:")]
    public void GivenTheGlobalConfigIs(string content) => _globalConfig.Clear().AppendLine(Normalize(content));

    [Given("the global config contains the line {string}")]
    public void GivenTheGlobalConfigContainsTheLine(string line) => _globalConfig.AppendLine(line);

    [Given("the library config contains the line {string}")]
    public void GivenTheLibraryConfigContainsTheLine(string line) => _libraryConfig.AppendLine(line);

    [Given("the directory {string} is a library")]
    public void GivenTheDirectoryIsALibrary(string path) => _directories.Add(Path.Combine(path, ".shelf"));

    [Given("the working directory is {string}")]
    public void GivenTheWorkingDirectoryIs(string path) => _workingDirectory = path;

    [When("shelf resolves the settings")]
    public void WhenShelfResolvesTheSettings()
    {
        try
        {
            _settings = Resolve();
        }
        catch (ConfigException e)
        {
            _settingsError = e;
        }
    }

    [When("shelf locates the library with the option {string}")]
    public void WhenShelfLocatesTheLibraryWithTheOption(string option)
    {
        var settings = Resolve();
        try
        {
            _resolution = LibraryLocator.Resolve(
                option.Length == 0 ? null : option,
                _workingDirectory,
                settings,
                _directories.Contains);
        }
        catch (LibraryNotFoundException e)
        {
            _locateError = e;
        }
    }

    [Then("the setting {string} is {string}")]
    public void ThenTheSettingIs(string key, string expected)
    {
        var grouping = Settings().Grouping;
        var actual = key switch
        {
            "grouping.auto_merge" => grouping.AutoMerge.ToString(CultureInfo.InvariantCulture),
            "grouping.review" => grouping.Review.ToString(CultureInfo.InvariantCulture),
            "grouping.max_hamming" => grouping.MaxHamming.ToString(CultureInfo.InvariantCulture),
            "grouping.auto_hamming" => grouping.AutoHamming.ToString(CultureInfo.InvariantCulture),
            "organize.pattern" => Settings().Organize.Pattern.Text,
            "semantic.model" => Settings().Semantic.Model,
            _ => throw new ArgumentException($"Unknown setting '{key}'.", nameof(key)),
        };
        Assert.Equal(expected, actual);
    }

    [Then("there are no warnings")]
    public void ThenThereAreNoWarnings() => Assert.Empty(Settings().Warnings);

    [Then("there is the warning {string}")]
    public void ThenThereIsTheWarning(string warning) => Assert.Contains(warning, Settings().Warnings);

    [Then("resolving the settings fails with an error containing {string}")]
    public void ThenResolvingTheSettingsFailsWithAnErrorContaining(string text)
    {
        Assert.NotNull(_settingsError);
        Assert.Contains(text, _settingsError.Message, StringComparison.Ordinal);
    }

    [Then("the library {string} is at {string}")]
    public void ThenTheLibraryIsAt(string name, string path) =>
        Assert.Equal(path, Assert.Single(Settings().Libraries, l => l.Name == name).Path);

    [Then("the default library is {string}")]
    public void ThenTheDefaultLibraryIs(string name) => Assert.Equal(name, Settings().DefaultLibrary);

    [Then("there are no libraries")]
    public void ThenThereAreNoLibraries() => Assert.Empty(Settings().Libraries);

    [Then("there is no default library")]
    public void ThenThereIsNoDefaultLibrary() => Assert.Null(Settings().DefaultLibrary);

    [Then("the library root is {string}")]
    public void ThenTheLibraryRootIs(string root)
    {
        Assert.Null(_locateError);
        Assert.Equal(root, _resolution?.Root);
    }

    [Then("the library comes from the {string}")]
    public void ThenTheLibraryComesFromThe(string source)
    {
        var expected = source switch
        {
            "option" => Shelf.Core.Libraries.LibrarySource.Option,
            "working directory" => Shelf.Core.Libraries.LibrarySource.WorkingDirectory,
            "default library" => Shelf.Core.Libraries.LibrarySource.DefaultLibrary,
            _ => throw new ArgumentException($"Unknown library source '{source}'.", nameof(source)),
        };
        Assert.Equal(expected, _resolution?.Source);
    }

    [Then("locating the library fails with an error containing {string}")]
    public void ThenLocatingTheLibraryFailsWithAnErrorContaining(string text)
    {
        Assert.NotNull(_locateError);
        Assert.Contains(text, _locateError.Message, StringComparison.Ordinal);
    }

    private ShelfSettings Settings()
    {
        Assert.Null(_settingsError);
        Assert.NotNull(_settings);
        return _settings;
    }

    private ShelfSettings Resolve() => ShelfSettings.Resolve(
        Parse(_globalConfig, GlobalSource),
        Parse(_libraryConfig, LibrarySource));

    private static ConfigDocument? Parse(StringBuilder text, string source) =>
        text.Length == 0 ? null : ConfigFile.Parse(text.ToString(), source);

    private static string Normalize(string content) => content.Replace("\r\n", "\n", StringComparison.Ordinal);
}
