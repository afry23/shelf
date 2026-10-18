using Reqnroll;
using Xunit;

namespace Shelf.Specs.Steps;

/// <summary>Steps for <c>shelf model install</c>: a catalog of tiny files and a downloader that never touches the network.</summary>
[Binding]
public sealed class ModelSteps(ScenarioEnvironment environment, ConfigSteps workspace)
{
    private FakeModels Models => environment.Models ?? throw new InvalidOperationException("No model catalog in this scenario.");

    [Given("the model catalog {string} has the files:")]
    public void GivenTheModelCatalogHasTheFiles(string name, DataTable files) =>
        environment.Models = new FakeModels(name, files.Rows.ToDictionary(r => r["file"], r => r["content"]));

    [Given("the folder {string} holds the model files")]
    public void GivenTheFolderHoldsTheModelFiles(string folder) => Write(folder, except: null);

    [Given("the folder {string} holds the model files except {string}")]
    public void GivenTheFolderHoldsTheModelFilesExcept(string folder, string missing) => Write(folder, missing);

    [Given("the download serves the model files")]
    public void GivenTheDownloadServesTheModelFiles() => Assert.Empty(Models.Overrides);

    [Given("the download fails with {string}")]
    public void GivenTheDownloadFailsWith(string message) => Models.FailWith = message;

    [Given("the download serves {string} for {string}")]
    public void GivenTheDownloadServesFor(string content, string file) => Models.Overrides[file] = content;

    [Then("no download was started")]
    public void ThenNoDownloadWasStarted() => Assert.Empty(Models.Requested);

    [Then("the download requested {string}")]
    public void ThenTheDownloadRequested(string url) => Assert.Contains(url, Models.Requested);

    [Given("the config file of the library {string} contains the line {string}")]
    public void GivenTheConfigFileOfTheLibraryContainsTheLine(string library, string line)
    {
        var path = Path.Combine(workspace.WorkingDirectory, library, ".shelf", "config.toml");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.AppendAllText(path, line + "\n");
    }

    private void Write(string folder, string? except)
    {
        var directory = Path.Combine(workspace.WorkingDirectory, folder);
        Directory.CreateDirectory(directory);
        foreach (var (name, content) in Models.Contents.Where(c => c.Key != except))
        {
            File.WriteAllText(Path.Combine(directory, name), content);
        }
    }
}
