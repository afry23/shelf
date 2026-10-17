using System.Text;
using Reqnroll;
using Xunit;

namespace Shelf.Specs.Steps;

/// <summary>Files and folders inside the scenario's working directory ("an empty working directory").</summary>
[Binding]
public sealed class WorkspaceSteps(ConfigSteps workspace)
{
    [Given("a file {string} in the working directory")]
    public void GivenAFileInTheWorkingDirectory(string name) => WriteFile(name, "data");

    [Given("a global config file with content:")]
    public void GivenAGlobalConfigFileWithContent(string content) =>
        WriteFile(Path.Combine(CliSteps.HomeFolder, ".config", "shelf", "config.toml"), content.Replace("\r\n", "\n", StringComparison.Ordinal));

    [Given("a work folder {string} with the files {string} and the sidecar:")]
    public void GivenAWorkFolder(string folder, string files, string sidecar)
    {
        foreach (var name in files.Split(", "))
        {
            WriteFile(Path.Combine(folder, name), "data");
        }

        WriteFile(Path.Combine(folder, "book.json"), sidecar.Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    [Given("the sidecar {string} is replaced by:")]
    public void GivenTheSidecarIsReplacedBy(string path, string content) =>
        WriteFile(path, content.Replace("\r\n", "\n", StringComparison.Ordinal));

    [Given("I delete the file {string}")]
    public void GivenIDeleteTheFile(string name)
    {
        foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
        {
            File.Delete(PathOf(name) + suffix);
        }
    }

    [Then("the file {string} does not exist")]
    public void ThenTheFileDoesNotExist(string name) => Assert.False(File.Exists(PathOf(name)), $"Unexpected file {name}");

    [Then("the directory {string} exists")]
    public void ThenTheDirectoryExists(string name) => Assert.True(Directory.Exists(PathOf(name)), $"Missing directory {name}");

    [Then("the directory {string} does not exist")]
    public void ThenTheDirectoryDoesNotExist(string name) => Assert.False(Directory.Exists(PathOf(name)), $"Unexpected directory {name}");

    private void WriteFile(string name, string content)
    {
        var path = PathOf(name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, new UTF8Encoding(false));
    }

    private string PathOf(string name) => Path.Combine(workspace.WorkingDirectory, name);
}
