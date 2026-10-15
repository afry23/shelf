using System.Globalization;
using System.Security.Cryptography;
using Reqnroll;
using Shelf.TestData;
using Xunit;

namespace Shelf.Specs.Steps;

[Binding]
public sealed class FixtureSteps
{
    private FixtureCatalog? _catalog;
    private string? _repositoryRoot;
    private string? _regenerated;

    private FixtureCatalog Catalog => _catalog ?? throw new InvalidOperationException("Missing step: the fixture manifest");

    [AfterScenario]
    public void DeleteRegenerated()
    {
        if (_regenerated is not null && Directory.Exists(_regenerated))
        {
            Directory.Delete(_regenerated, recursive: true);
        }
    }

    [Given("the fixture manifest {string}")]
    public void GivenTheFixtureManifest(string relativePath)
    {
        Assert.Equal(FixtureCatalog.ManifestRelativePath, relativePath);
        _repositoryRoot = RepositoryPaths.Root();
        _catalog = FixtureCatalog.Load(_repositoryRoot);
    }

    [Then("every file under {string} except {string} and {string} is listed in the manifest")]
    public void ThenEveryFileIsListed(string directory, string firstExcluded, string secondExcluded)
    {
        var root = Path.Combine(_repositoryRoot!, directory);
        var onDisk = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
            .Where(f => f != firstExcluded && f != secondExcluded)
            .Order(StringComparer.Ordinal);
        Assert.Equal(Catalog.Fixtures.Select(f => f.Path).Order(StringComparer.Ordinal), onDisk);
    }

    [Then("every file listed in the manifest exists")]
    public void ThenEveryListedFileExists()
    {
        foreach (var entry in Catalog.Fixtures)
        {
            Assert.True(File.Exists(Catalog.FullPath(entry)), entry.Path);
        }
    }

    [Then("every fixture file has the size and SHA-256 recorded in the manifest")]
    public void ThenEveryFixtureHasTheRecordedSizeAndHash()
    {
        foreach (var entry in Catalog.Fixtures)
        {
            var bytes = File.ReadAllBytes(Catalog.FullPath(entry));
            Assert.Equal(entry.Size, bytes.Length);
            Assert.Equal(entry.Sha256, Convert.ToHexStringLower(SHA256.HashData(bytes)));
        }
    }

    [Then("the total size of all fixture files is at most {long} bytes")]
    public void ThenTheTotalSizeIsAtMost(long limit)
    {
        var total = Catalog.Fixtures.Sum(f => new FileInfo(Catalog.FullPath(f)).Length);
        Assert.True(total <= limit, $"Fixtures use {total} bytes, the budget is {limit}.");
        Assert.Equal(limit, Catalog.Budget.TotalBytes);
    }

    [Then("no fixture file is larger than {long} bytes")]
    public void ThenNoFixtureIsLargerThan(long limit)
    {
        foreach (var entry in Catalog.Fixtures)
        {
            var size = new FileInfo(Catalog.FullPath(entry)).Length;
            Assert.True(size <= limit, $"{entry.Path} has {size} bytes, the limit is {limit}.");
        }

        Assert.Equal(limit, Catalog.Budget.FileBytes);
    }

    [Then("every fixture of origin {string} has a source URL, a retrieval date, a license, a US public-domain basis and a life+70 basis")]
    public void ThenEveryFixtureHasProvenance(string origin)
    {
        var entries = OfOrigin(origin);
        foreach (var entry in entries)
        {
            Assert.NotNull(entry.Source);
            Assert.True(Uri.TryCreate(entry.Source!.Url, UriKind.Absolute, out var uri) && uri.Scheme == "https", entry.Path + ": source URL");
            Assert.True(
                DateTime.TryParseExact(entry.Source.Retrieved, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
                entry.Path + ": retrieval date");
            Assert.False(string.IsNullOrWhiteSpace(entry.License), entry.Path + ": license");
            Assert.NotNull(entry.PublicDomain);
            Assert.False(string.IsNullOrWhiteSpace(entry.PublicDomain!.Us), entry.Path + ": US public-domain basis");
            Assert.False(string.IsNullOrWhiteSpace(entry.PublicDomain.LifePlus70), entry.Path + ": life+70 basis");
        }
    }

    [Then("every fixture of origin {string} is unmodified")]
    public void ThenEveryFixtureIsUnmodified(string origin)
    {
        foreach (var entry in OfOrigin(origin))
        {
            Assert.Empty(entry.Modifications);
        }
    }

    [Then("every fixture of origin {string} names its generator command and has no source URL")]
    public void ThenEveryFixtureNamesItsGeneratorCommand(string origin)
    {
        foreach (var entry in OfOrigin(origin))
        {
            Assert.False(string.IsNullOrWhiteSpace(entry.GeneratorCommand), entry.Path);
            Assert.True(string.IsNullOrEmpty(entry.Source?.Url), entry.Path);
        }
    }

    [Then("at least one fixture of origin {string} has the trait {string}")]
    public void ThenAtLeastOneFixtureHasTheTrait(string origin, string trait) =>
        Assert.Contains(Selected(origin), f => f.Traits.Contains(trait));

    [Then("the fixtures contain a work of origin {string} with files of the traits {string} and {string}")]
    public void ThenTheFixturesContainAWorkWithTraits(string origin, string first, string second)
    {
        var found = Selected(origin).GroupBy(f => f.Work, StringComparer.Ordinal).Any(group =>
        {
            var members = group.ToList();
            return members.Any(a => a.Traits.Contains(first)
                && members.Any(b => !ReferenceEquals(a, b) && b.Traits.Contains(second)));
        });
        Assert.True(found, $"No {origin} work has files with the traits '{first}' and '{second}'.");
    }

    [When("the derived fixtures are regenerated into a temporary directory")]
    public void WhenTheDerivedFixturesAreRegenerated()
    {
        _regenerated = Path.Combine(Path.GetTempPath(), "shelf-specs", Guid.NewGuid().ToString("N"));
        var exit = GeneratorApp.Run(["fixtures", "--out", _regenerated], new StringWriter(), new StringWriter());
        Assert.Equal(0, exit);
    }

    [Then("every regenerated file is byte-identical to the committed derived fixture with the same name")]
    public void ThenEveryRegeneratedFileIsIdentical()
    {
        var committed = Path.Combine(Catalog.Directory, "derived");
        foreach (var file in Directory.EnumerateFiles(_regenerated!))
        {
            var name = Path.GetFileName(file);
            var target = Path.Combine(committed, name);
            Assert.True(File.Exists(target), $"{name} is not committed");
            Assert.True(File.ReadAllBytes(file).AsSpan().SequenceEqual(File.ReadAllBytes(target)), $"{name} differs");
        }
    }

    [Then("no committed derived fixture is missing from the regenerated set")]
    public void ThenNoCommittedDerivedFixtureIsMissing()
    {
        var committed = Directory.EnumerateFiles(Path.Combine(Catalog.Directory, "derived")).Select(Path.GetFileName).Order(StringComparer.Ordinal);
        var regenerated = Directory.EnumerateFiles(_regenerated!).Select(Path.GetFileName).Order(StringComparer.Ordinal);
        Assert.Equal(committed, regenerated);
    }

    private List<FixtureEntry> OfOrigin(string origin)
    {
        var entries = Catalog.Fixtures.Where(f => f.Origin == origin).ToList();
        Assert.NotEmpty(entries);
        return entries;
    }

    private List<FixtureEntry> Selected(string origin) =>
        origin == "any" ? [.. Catalog.Fixtures] : OfOrigin(origin);
}
