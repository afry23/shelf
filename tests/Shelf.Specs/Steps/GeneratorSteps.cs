using System.Security.Cryptography;
using System.Text.Json;
using Reqnroll;
using Shelf.TestData;
using Xunit;

namespace Shelf.Specs.Steps;

// Steps for tests/Shelf.TestData (the synthetic library generator). Phrases start with "the generator", "the output",
// "the ground truth" or "fixture" so they cannot clash with CliSteps (a shelf run).
[Binding]
public sealed class GeneratorSteps
{
    private string? _workspace;
    private readonly StringWriter _stdout = new();
    private readonly StringWriter _stderr = new();
    private int _exitCode = -1;

    private string Workspace => _workspace ?? throw new InvalidOperationException("Missing step: an empty generator workspace");

    [AfterScenario]
    public void DeleteWorkspace()
    {
        if (_workspace is not null && Directory.Exists(_workspace))
        {
            Directory.Delete(_workspace, recursive: true);
        }
    }

    [Given("an empty generator workspace")]
    public void GivenAnEmptyGeneratorWorkspace()
    {
        _workspace = Path.Combine(Path.GetTempPath(), "shelf-specs", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workspace);
    }

    [Given("the generator workspace directory {string} contains a file {string}")]
    public void GivenTheGeneratorWorkspaceDirectoryContainsAFile(string directory, string fileName)
    {
        var path = Path.Combine(Workspace, directory);
        Directory.CreateDirectory(path);
        File.WriteAllBytes(Path.Combine(path, fileName), [0x20]);
    }

    [When("I run the generator with arguments {string}")]
    public void WhenIRunTheGeneratorWithArguments(string arguments) => Run(arguments, null);

    [When("I run the generator with arguments {string} into {string}")]
    public void WhenIRunTheGeneratorWithArgumentsInto(string arguments, string directory) => Run(arguments, directory);

    [Then("the generator exit code is {int}")]
    public void ThenTheGeneratorExitCodeIs(int expected) => Assert.Equal(expected, _exitCode);

    [Then("the generator error output contains {string}")]
    public void ThenTheGeneratorErrorOutputContains(string text) =>
        Assert.Contains(text, _stderr.ToString(), StringComparison.Ordinal);

    [Then("the generator plan has the value {int} for {string}")]
    public void ThenTheGeneratorPlanHasTheValueFor(int expected, string property)
    {
        using var document = JsonDocument.Parse(_stdout.ToString());
        Assert.Equal(expected, document.RootElement.GetProperty(property).GetInt32());
    }

    [Then("the output {string} contains {int} e-book files")]
    public void ThenTheOutputContainsEBookFiles(string directory, int expected) =>
        Assert.Equal(expected, BookFiles(directory).Count);

    [Then("the output {string} contains {int} EPUB files and {int} PDF files")]
    public void ThenTheOutputContainsEpubAndPdfFiles(string directory, int epubs, int pdfs)
    {
        var files = BookFiles(directory);
        Assert.Equal(epubs, files.Count(f => f.EndsWith(".epub", StringComparison.Ordinal)));
        Assert.Equal(pdfs, files.Count(f => f.EndsWith(".pdf", StringComparison.Ordinal)));
    }

    [Then("the ground truth of {string} lists {int} works and {int} files")]
    public void ThenTheGroundTruthListsWorksAndFiles(string directory, int works, int files)
    {
        var truth = Truth(directory);
        Assert.Equal(works, truth.Works.Count);
        Assert.Equal(files, truth.Files.Count);
    }

    [Then("the ground truth of {string} lists {int} works with 1 file, {int} works with 2 files and {int} works with 3 files")]
    public void ThenTheGroundTruthListsWorksByFileCount(string directory, int single, int twofold, int threefold)
    {
        var truth = Truth(directory);
        Assert.Equal(single, truth.Works.Count(w => w.Files.Count == 1));
        Assert.Equal(twofold, truth.Works.Count(w => w.Files.Count == 2));
        Assert.Equal(threefold, truth.Works.Count(w => w.Files.Count == 3));
    }

    [Then("the outputs {string} and {string} are byte-identical")]
    public void ThenTheOutputsAreByteIdentical(string first, string second)
    {
        var a = AllFiles(first);
        var b = AllFiles(second);
        Assert.Equal(a, b);
        foreach (var relative in a)
        {
            Assert.True(
                File.ReadAllBytes(Path.Combine(Workspace, first, relative)).AsSpan()
                    .SequenceEqual(File.ReadAllBytes(Path.Combine(Workspace, second, relative))),
                $"'{relative}' differs");
        }
    }

    [Then("the outputs {string} and {string} differ")]
    public void ThenTheOutputsDiffer(string first, string second)
    {
        var a = AllFiles(first);
        var b = AllFiles(second);
        var identical = a.SequenceEqual(b, StringComparer.Ordinal) && a.All(relative =>
            File.ReadAllBytes(Path.Combine(Workspace, first, relative)).AsSpan()
                .SequenceEqual(File.ReadAllBytes(Path.Combine(Workspace, second, relative))));
        Assert.False(identical, "The two outputs are identical.");
    }

    [Then("every e-book file in {string} is listed in the ground truth with its size and SHA-256")]
    public void ThenEveryEBookFileIsListed(string directory)
    {
        var truth = Truth(directory);
        var listed = truth.Files.ToDictionary(f => f.Path, StringComparer.Ordinal);
        var onDisk = BookFiles(directory);
        Assert.Equal(onDisk.Order(StringComparer.Ordinal), listed.Keys.Order(StringComparer.Ordinal));
        foreach (var relative in onDisk)
        {
            var bytes = File.ReadAllBytes(Path.Combine(Workspace, directory, relative));
            Assert.Equal(listed[relative].Size, bytes.Length);
            Assert.Equal(listed[relative].Sha256, Convert.ToHexStringLower(SHA256.HashData(bytes)));
        }
    }

    [Then("every file listed in the ground truth of {string} exists")]
    public void ThenEveryFileListedExists(string directory)
    {
        foreach (var file in Truth(directory).Files)
        {
            Assert.True(File.Exists(Path.Combine(Workspace, directory, file.Path)), file.Path);
        }
    }

    [Then("every generated EPUB in {string} starts with the ZIP signature and every generated PDF with {string}")]
    public void ThenEveryGeneratedFileStartsWithItsSignature(string directory, string pdfPrefix)
    {
        byte[] zip = [0x50, 0x4B, 0x03, 0x04];
        var pdf = System.Text.Encoding.ASCII.GetBytes(pdfPrefix);
        foreach (var relative in BookFiles(directory))
        {
            var bytes = File.ReadAllBytes(Path.Combine(Workspace, directory, relative));
            var expected = relative.EndsWith(".epub", StringComparison.Ordinal) ? zip : pdf;
            Assert.True(bytes.AsSpan().StartsWith(expected), relative);
        }
    }

    [Then("the ground truth of {string} lists {int} files with duplicate kind {string}")]
    public void ThenTheGroundTruthListsFilesWithDuplicateKind(string directory, int count, string kind) =>
        Assert.Equal(count, Truth(directory).Files.Count(f => f.DuplicateKind == kind));

    [Then("every file with duplicate kind {string} in {string} belongs to the same work as its original")]
    public void ThenEveryDuplicateBelongsToTheSameWork(string kind, string directory)
    {
        var truth = Truth(directory);
        var byPath = truth.Files.ToDictionary(f => f.Path, StringComparer.Ordinal);
        var duplicates = truth.Files.Where(f => f.DuplicateKind == kind).ToList();
        Assert.NotEmpty(duplicates);
        foreach (var file in duplicates)
        {
            Assert.NotNull(file.DuplicateOf);
            Assert.Equal(byPath[file.DuplicateOf!].Work, file.Work);
        }
    }

    [Then("every file with duplicate kind {string} in {string} has the same SHA-256 as its original")]
    public void ThenEveryDuplicateHasTheSameHash(string kind, string directory) =>
        AssertDuplicateHashes(kind, directory, same: true);

    [Then("every file with duplicate kind {string} in {string} has a different SHA-256 than its original")]
    public void ThenEveryDuplicateHasADifferentHash(string kind, string directory) =>
        AssertDuplicateHashes(kind, directory, same: false);

    [Then("the ground truth of {string} has at least one file with the trait {string}")]
    public void ThenTheGroundTruthHasAFileWithTheTrait(string directory, string trait) =>
        Assert.Contains(Truth(directory).Files, f => f.Traits.Contains(trait));

    [Then("the ground truth of {string} lists {int} work pairs with relation {string}")]
    public void ThenTheGroundTruthListsWorkPairs(string directory, int count, string relation) =>
        Assert.Equal(count, Truth(directory).Relations.Count(r => r.Kind == relation));

    [Then("the generator workspace file {string} still exists")]
    public void ThenTheGeneratorWorkspaceFileStillExists(string relativePath) =>
        Assert.True(File.Exists(Path.Combine(Workspace, relativePath)));

    private void Run(string arguments, string? directory)
    {
        var args = arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (directory is not null)
        {
            args.Add("--out");
            args.Add(Path.Combine(Workspace, directory));
        }

        _stdout.GetStringBuilder().Clear();
        _stderr.GetStringBuilder().Clear();
        _exitCode = GeneratorApp.Run([.. args], _stdout, _stderr);
    }

    private GroundTruth Truth(string directory) => GroundTruth.Load(Path.Combine(Workspace, directory, "ground-truth.json"));

    private List<string> BookFiles(string directory)
    {
        var input = Path.Combine(Workspace, directory, "input");
        if (!Directory.Exists(input))
        {
            return [];
        }

        return [.. Directory.EnumerateFiles(input, "*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".epub", StringComparison.Ordinal) || f.EndsWith(".pdf", StringComparison.Ordinal))
            .Select(f => Path.GetRelativePath(Path.Combine(Workspace, directory), f).Replace('\\', '/'))];
    }

    private List<string> AllFiles(string directory)
    {
        var root = Path.Combine(Workspace, directory);
        return [.. Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)];
    }

    private void AssertDuplicateHashes(string kind, string directory, bool same)
    {
        var truth = Truth(directory);
        var byPath = truth.Files.ToDictionary(f => f.Path, StringComparer.Ordinal);
        var duplicates = truth.Files.Where(f => f.DuplicateKind == kind).ToList();
        Assert.NotEmpty(duplicates);
        foreach (var file in duplicates)
        {
            var original = byPath[file.DuplicateOf!];
            if (same)
            {
                Assert.Equal(original.Sha256, file.Sha256);
            }
            else
            {
                Assert.NotEqual(original.Sha256, file.Sha256);
            }
        }
    }
}
