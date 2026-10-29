using System.Text;
using Reqnroll;
using Shelf.TestData;
using Xunit;

namespace Shelf.Specs.Steps;

/// <summary>Files and folders inside the scenario's working directory ("an empty working directory").</summary>
[Binding]
public sealed class WorkspaceSteps(ConfigSteps workspace)
{
    private Dictionary<string, string>? _snapshot;

    [Given("the file {string} is copied to {string}")]
    public void GivenTheFileIsCopiedTo(string from, string to) => File.Copy(PathOf(from), PathOf(to));

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

    [Given("the folder {string} holds {int} works titled {string} followed by a number")]
    public void GivenTheFolderHoldsWorks(string folder, int count, string title)
    {
        for (var number = 1; number <= count; number++)
        {
            var name = $"{title} {number:D3}";
            var id = "01J9Z4" + number.ToString("D20", System.Globalization.CultureInfo.InvariantCulture);
            WriteFile(Path.Combine(folder, name, "note.txt"), "data");
            WriteFile(Path.Combine(folder, name, "book.json"), $$"""{ "schema": 1, "id": "{{id}}", "title": "{{name}}", "authors": ["Filler Author"], "files": [{ "name": "note.txt" }] }""" + "\n");
        }
    }

    [Given("the sidecar {string} is replaced by:")]
    public void GivenTheSidecarIsReplacedBy(string path, string content) =>
        WriteFile(path, content.Replace("\r\n", "\n", StringComparison.Ordinal));

    [Given("I delete the file {string}")]
    [When("I delete the file {string}")]
    public void GivenIDeleteTheFile(string name)
    {
        foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
        {
            File.Delete(PathOf(name) + suffix);
        }
    }

    [Given("the file {string} has the content {string}")]
    public void GivenTheFileHasTheContent(string name, string content) => WriteFile(name, content);

    [Given("a work folder {string} with the fixture files {string} and the sidecar:")]
    public void GivenAWorkFolderWithFixtures(string folder, string files, string sidecar)
    {
        foreach (var entry in files.Split(", "))
        {
            var parts = entry.Split('=');
            CopyFixture(parts[1], Path.Combine(folder, parts[0]));
        }

        WriteFile(Path.Combine(folder, "book.json"), sidecar.Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    [Given("the folder {string} contains the fixtures {string}")]
    public void GivenTheFolderContainsTheFixtures(string folder, string fixtures)
    {
        foreach (var fixture in fixtures.Split(", "))
        {
            CopyFixture(fixture, Path.Combine(folder, Path.GetFileName(fixture)));
        }
    }

    [Given("the file {string} is replaced by the fixture {string}")]
    public void GivenTheFileIsReplacedByTheFixture(string name, string fixture)
    {
        var path = PathOf(name);
        var modified = File.GetLastWriteTimeUtc(path);
        File.Copy(FixturePath(fixture), path, overwrite: true);
        // A change of content comes with a change of time (the scan looks at size and time first).
        File.SetLastWriteTimeUtc(path, modified.AddMinutes(5));
    }

    [Given("the file {string} has a new modification time")]
    public void GivenTheFileHasANewModificationTime(string name)
    {
        var path = PathOf(name);
        File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(path).AddHours(2));
    }

    [Given("I delete the folder {string}")]
    public void GivenIDeleteTheFolder(string name) => Directory.Delete(PathOf(name), recursive: true);

    [Given("I move the folder {string} to {string}")]
    public void GivenIMoveTheFolder(string from, string to) => Directory.Move(PathOf(from), PathOf(to));

    [Given("a sample PDF {string} with the title {string}, the author {string} and the text seed {int}")]
    public void GivenASamplePdf(string name, string title, string author, int seed)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathOf(name))!);
        File.WriteAllBytes(PathOf(name), SampleBooks.Pdf(title, author, isbn13: null, seed));
    }

    [Given("a sample PDF {string} with the title {string}, the author {string}, the ISBN {string} and the text seed {int}")]
    public void GivenASamplePdfWithIsbn(string name, string title, string author, string isbn, int seed)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathOf(name))!);
        File.WriteAllBytes(PathOf(name), SampleBooks.Pdf(title, author, isbn, seed));
    }

    [Given("a sample EPUB {string} with the title {string}, the authors {string} and the text seed {int}")]
    public void GivenASampleEpub(string name, string title, string authors, int seed)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathOf(name))!);
        File.WriteAllBytes(PathOf(name), SampleBooks.Epub(title, authors.Split("; "), isbn13: null, seed));
    }

    [Given("a sample EPUB {string} with the title {string}, the authors {string} and the text seed {int} with {int} words changed")]
    public void GivenASampleEpubWithChangedWords(string name, string title, string authors, int seed, int changed)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathOf(name))!);
        File.WriteAllBytes(PathOf(name), SampleBooks.EpubWithChangedWords(title, authors.Split("; "), seed, changed));
    }

    [Given("a sample EPUB {string} with the title {string}, the authors {string}, the ISBN {string} and the text seed {int}")]
    public void GivenASampleEpubWithIsbn(string name, string title, string authors, string isbn, int seed)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathOf(name))!);
        File.WriteAllBytes(PathOf(name), SampleBooks.Epub(title, authors.Split("; "), isbn, seed));
    }

    [Then("the file {string} exists")]
    public void ThenTheFileExists(string name) => Assert.True(File.Exists(PathOf(name)), $"Missing file {name}");

    [Then("the file {string} has the content {string}")]
    public void ThenTheFileHasTheContent(string name, string content) => Assert.Equal(content, File.ReadAllText(PathOf(name)));

    [Given("the global config file contains the line {string}")]
    public void GivenTheGlobalConfigFileContainsTheLine(string line)
    {
        var path = PathOf(Path.Combine(CliSteps.HomeFolder, ".config", "shelf", "config.toml"));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.AppendAllText(path, line + "\n");
    }

    [Then("the file {string} does not exist")]
    public void ThenTheFileDoesNotExist(string name) => Assert.False(File.Exists(PathOf(name)), $"Unexpected file {name}");

    [Then("the directory {string} exists")]
    public void ThenTheDirectoryExists(string name) => Assert.True(Directory.Exists(PathOf(name)), $"Missing directory {name}");

    [Then("the directory {string} does not exist")]
    public void ThenTheDirectoryDoesNotExist(string name) => Assert.False(Directory.Exists(PathOf(name)), $"Unexpected directory {name}");

    [Given("I take a snapshot of the folder {string}")]
    public void GivenISnapshot(string folder) => _snapshot = Snapshot(PathOf(folder));

    [Then("the folder {string} equals the snapshot")]
    public void ThenTheFolderEqualsTheSnapshot(string folder)
    {
        Assert.NotNull(_snapshot);
        var now = Snapshot(PathOf(folder));
        Assert.Equal(
            _snapshot.Select(p => $"{p.Key} {p.Value}").Order(StringComparer.Ordinal),
            now.Select(p => $"{p.Key} {p.Value}").Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// Every file (SHA-256) and every folder below <paramref name="root"/>, except the index database and the
    /// journal: the state that "undo restores exactly" (acceptance criterion 2).
    /// </summary>
    private static Dictionary<string, string> Snapshot(string root)
    {
        var snapshot = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, entry).Replace(Path.DirectorySeparatorChar, '/');
            if (relative.StartsWith(".shelf/index.db", StringComparison.Ordinal) || relative.StartsWith(".shelf/journal", StringComparison.Ordinal))
            {
                continue;
            }

            snapshot[relative] = Directory.Exists(entry)
                ? "<folder>"
                : Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(entry)));
        }

        return snapshot;
    }

    private void CopyFixture(string fixture, string target)
    {
        var path = PathOf(target);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.Copy(FixturePath(fixture), path);
    }

    private static string FixturePath(string relative) => Path.Combine(RepositoryPaths.Root(), "tests", "fixtures", relative);

    private void WriteFile(string name, string content)
    {
        var path = PathOf(name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, new UTF8Encoding(false));
    }

    private string PathOf(string name) => Path.Combine(workspace.WorkingDirectory, name);
}
