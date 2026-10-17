using System.Text;
using System.Text.Json;
using Reqnroll;
using Shelf.Core.Model;
using Xunit;

namespace Shelf.Specs.Steps;

[Binding]
public sealed class SidecarSteps(ConfigSteps workspace)
{
    private WorkSidecar? _current;
    private WorkSidecar? _written;
    private WorkSidecar? _read;
    private SidecarException? _readError;
    private SidecarException? _writeError;

    [Given("a sidecar {string} with content:")]
    public void GivenASidecarWithContent(string name, string content) =>
        File.WriteAllText(PathOf(name), content.Replace("\r\n", "\n", StringComparison.Ordinal), new UTF8Encoding(false));

    [Given("a new sidecar for the work {string} by {string} with the file {string}")]
    public void GivenANewSidecar(string title, string author, string file) =>
        _current = new WorkSidecar
        {
            Id = Ulid.New(),
            Title = title,
            Authors = [author],
            Files = [new SidecarFile(file)],
        };

    [Given("shelf has written the sidecar to {string}")]
    public void GivenShelfHasWrittenTheSidecarTo(string name) => WhenShelfWritesTheSidecarTo(name);

    [When("shelf writes the sidecar to {string}")]
    public void WhenShelfWritesTheSidecarTo(string name)
    {
        Assert.NotNull(_current);
        try
        {
            SidecarStore.Write(PathOf(name), _current);
            _written = _current;
        }
        catch (SidecarException e)
        {
            _writeError = e;
        }
    }

    [When("shelf reads the sidecar {string}")]
    public void WhenShelfReadsTheSidecar(string name)
    {
        try
        {
            _read = SidecarStore.Read(PathOf(name));
            _current = _read;
        }
        catch (SidecarException e)
        {
            _readError = e;
        }
    }

    [When("shelf changes the title to {string}")]
    public void WhenShelfChangesTheTitleTo(string title) => _current = Current() with { Title = title };

    [Then("reading the sidecar succeeds")]
    public void ThenReadingSucceeds()
    {
        Assert.Null(_readError);
        Assert.NotNull(_read);
    }

    [Then("reading the sidecar fails with an error containing {string}")]
    public void ThenReadingFailsWithAnErrorContaining(string text)
    {
        Assert.NotNull(_readError);
        Assert.Contains(text, _readError.Message, StringComparison.Ordinal);
    }

    [Then("writing fails with an error containing {string}")]
    public void ThenWritingFailsWithAnErrorContaining(string text)
    {
        Assert.NotNull(_writeError);
        Assert.Contains(text, _writeError.Message, StringComparison.Ordinal);
    }

    [Then("the sidecar title is {string}")]
    public void ThenTheSidecarTitleIs(string title) => Assert.Equal(title, Read().Title);

    [Then("the sidecar authors are {string}")]
    public void ThenTheSidecarAuthorsAre(string authors) => Assert.Equal(authors.Split(", "), Read().Authors);

    [Then("the sidecar tags are {string}")]
    public void ThenTheSidecarTagsAre(string tags) => Assert.Equal(tags.Split(", "), Read().Tags);

    [Then("the sidecar has no tags")]
    public void ThenTheSidecarHasNoTags() => Assert.Empty(Read().Tags);

    [Then("the sidecar has {int} files")]
    public void ThenTheSidecarHasFiles(int count) => Assert.Equal(count, Read().Files.Count);

    [Then("the progress of file {int} is {string} at {string}")]
    public void ThenTheProgressOfFileIs(int number, string locator, string updatedAt)
    {
        var progress = Read().Files[number - 1].Progress;
        Assert.NotNull(progress);
        Assert.Equal(locator, progress.Locator);
        Assert.Equal(DateTimeOffset.Parse(updatedAt, System.Globalization.CultureInfo.InvariantCulture), progress.UpdatedAt);
    }

    [Then("file {int} has no progress")]
    public void ThenFileHasNoProgress(int number) => Assert.Null(Read().Files[number - 1].Progress);

    [Then("the sidecar read status is {string}")]
    public void ThenTheSidecarReadStatusIs(string status) =>
        Assert.Equal(status, Read().ReadStatus.ToString().ToLowerInvariant());

    [Then("the sidecar marks {int} work(s) and {int} file pair(s) as not duplicates")]
    public void ThenTheSidecarMarksNotDuplicates(int works, int pairs)
    {
        Assert.Equal(works, Read().NotDuplicates.Works.Count);
        Assert.Equal(pairs, Read().NotDuplicates.Files.Count);
    }

    [Then("the sidecar read back equals the written one")]
    public void ThenTheSidecarReadBackEqualsTheWrittenOne()
    {
        Assert.NotNull(_written);
        Assert.Equal(SidecarFormat.Serialize(_written), SidecarFormat.Serialize(Read()));
    }

    [Then("the file {string} is indented UTF-8 text with LF line endings and a final newline")]
    public void ThenTheFileIsIndentedText(string name)
    {
        var bytes = File.ReadAllBytes(PathOf(name));
        Assert.False(bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }), "BOM");
        var text = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
        Assert.DoesNotContain('\r', text);
        Assert.EndsWith("}\n", text, StringComparison.Ordinal);
        Assert.Contains("\n  \"schema\": 1,\n", text, StringComparison.Ordinal);
    }

    [Then("the keys of {string} start with {string}")]
    public void ThenTheKeysStartWith(string name, string keys)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(PathOf(name)));
        var expected = keys.Split(", ");
        Assert.Equal(expected, document.RootElement.EnumerateObject().Take(expected.Length).Select(p => p.Name));
    }

    [Then("the file {string} contains {string}")]
    public void ThenTheFileContains(string name, string text) =>
        Assert.Contains(text, File.ReadAllText(PathOf(name)), StringComparison.Ordinal);

    [Then("the file {string} does not contain {string}")]
    public void ThenTheFileDoesNotContain(string name, string text) =>
        Assert.DoesNotContain(text, File.ReadAllText(PathOf(name)), StringComparison.Ordinal);

    [Then("no temporary files remain in the working directory")]
    public void ThenNoTemporaryFilesRemain() =>
        Assert.Empty(Directory.GetFiles(workspace.WorkingDirectory, "*.tmp"));

    private WorkSidecar Current() => _current ?? throw new InvalidOperationException("No sidecar in this scenario.");

    private WorkSidecar Read()
    {
        Assert.Null(_readError);
        return _read ?? throw new InvalidOperationException("Nothing was read in this scenario.");
    }

    private string PathOf(string name) => Path.Combine(workspace.WorkingDirectory, name);
}
