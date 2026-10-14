using Reqnroll;
using Shelf.Core.Configuration;
using Xunit;

namespace Shelf.Specs.Steps;

[Binding]
public sealed class ConfigLocationSteps(ScenarioEnvironment environment)
{
    private string? _home;
    private string? _path;

    [Given("the home directory is {string}")]
    public void GivenTheHomeDirectoryIs(string home) => _home = home;

    [When("shelf resolves the global config file path")]
    public void WhenShelfResolvesTheGlobalConfigFilePath()
    {
        Assert.NotNull(_home);
        _path = ConfigPaths.Global(name => environment.Variables.GetValueOrDefault(name), _home);
    }

    [When("shelf resolves the library config file path for the root {string}")]
    public void WhenShelfResolvesTheLibraryConfigFilePathForTheRoot(string root) =>
        _path = ConfigPaths.Library(root);

    [Then("the path is {string}")]
    public void ThenThePathIs(string expected) => Assert.Equal(expected, _path);
}
