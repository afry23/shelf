using System.Security.Cryptography;
using Reqnroll;
using Shelf.Core.Model;
using Shelf.TestData;
using Xunit;

namespace Shelf.Specs.Steps;

/// <summary>
/// Grades the grouping of an imported library against the ground truth of a generated one (acceptance criterion 1).
/// Files are matched by SHA-256, so renamed and relocated files and skipped exact copies need no special care.
/// </summary>
[Binding]
public sealed class CalibrationSteps(ConfigSteps workspace)
{
    private const string Generated = "generated";
    private const string Library = "library";

    private GroundTruth? _truth;

    [Given("a generated library of {int} works and {int} files with the seed {int}")]
    public void GivenAGeneratedLibrary(int works, int files, int seed)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = GeneratorApp.Run(
            ["generate", "--works", works.ToString(System.Globalization.CultureInfo.InvariantCulture), "--files", files.ToString(System.Globalization.CultureInfo.InvariantCulture), "--seed", seed.ToString(System.Globalization.CultureInfo.InvariantCulture), "--out", Path.Combine(workspace.WorkingDirectory, Generated)],
            output,
            error);
        Assert.True(code == 0, error.ToString());
        _truth = GroundTruth.Load(Path.Combine(workspace.WorkingDirectory, Generated, "ground-truth.json"));
    }

    [Given("a generated library of {int} works and {int} files with the seed {int} and the PDFs in print layout")]
    public void GivenAGeneratedLibraryInPrintLayout(int works, int files, int seed)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = GeneratorApp.Run(
            ["generate", "--works", works.ToString(System.Globalization.CultureInfo.InvariantCulture), "--files", files.ToString(System.Globalization.CultureInfo.InvariantCulture), "--seed", seed.ToString(System.Globalization.CultureInfo.InvariantCulture), "--print-layout", "--out", Path.Combine(workspace.WorkingDirectory, Generated)],
            output,
            error);
        Assert.True(code == 0, error.ToString());
        _truth = GroundTruth.Load(Path.Combine(workspace.WorkingDirectory, Generated, "ground-truth.json"));
    }

    [Then("the generated library has {int} works with an EPUB and a PDF")]
    public void ThenTheGeneratedLibraryHasPairs(int expected) => Assert.Equal(expected, Pairs().Count);

    [Then("at least {int} percent of them are in one work of the library")]
    public void ThenAtLeastPercentAreInOneWork(int percent)
    {
        var workOfSha = LibraryWorks();
        var pairs = Pairs();
        var merged = pairs.Count(p => workOfSha.TryGetValue(p.Epub, out var epub) && workOfSha.TryGetValue(p.Pdf, out var pdf) && epub == pdf);
        Assert.True(
            merged * 100 >= percent * pairs.Count,
            $"{merged} of {pairs.Count} format pairs are in one work; at least {percent} percent are required.");
    }

    [Then("no work of the library holds the files of different generated works")]
    public void ThenNoWorkHoldsDifferentWorks()
    {
        var truthOfSha = Truth().Files.GroupBy(f => f.Sha256).ToDictionary(g => g.Key, g => g.First().Work, StringComparer.Ordinal);
        var wrong = LibraryWorks()
            .GroupBy(kv => kv.Value)
            .Where(g => g.Select(kv => truthOfSha[kv.Key]).Distinct(StringComparer.Ordinal).Count() > 1)
            .Select(g => g.Key)
            .ToList();
        Assert.True(wrong.Count == 0, $"{wrong.Count} works hold files of different generated works: {string.Join(", ", wrong.Take(5))}");
    }

    [Then("no work of the library holds the files of different generated works other than editions of one book")]
    public void ThenNoWorkHoldsDifferentWorksOtherThanEditions()
    {
        var truth = Truth();
        var truthOfSha = truth.Files.GroupBy(f => f.Sha256).ToDictionary(g => g.Key, g => g.First().Work, StringComparer.Ordinal);
        var editions = truth.Relations.Where(r => r.Kind == "edition").Select(r => (r.A, r.B)).ToHashSet();
        var wrong = LibraryWorks()
            .GroupBy(kv => kv.Value)
            .Where(g =>
            {
                var generated = g.Select(kv => truthOfSha[kv.Key]).Distinct(StringComparer.Ordinal).ToList();
                return generated.Count > 1 && generated.Any(a => generated.Any(b => a != b && !editions.Contains((a, b)) && !editions.Contains((b, a))));
            })
            .Select(g => g.Key)
            .ToList();
        Assert.True(wrong.Count == 0, $"{wrong.Count} works hold files of different generated works: {string.Join(", ", wrong.Take(5))}");
    }

    [Then("the library has at most {int} works")]
    public void ThenTheLibraryHasAtMostWorks(int maximum)
    {
        var count = LibraryWorks().Values.Distinct().Count();
        Assert.True(count <= maximum, $"The library has {count} works; at most {maximum} are expected.");
    }

    private GroundTruth Truth() => _truth ?? throw new InvalidOperationException("Missing step: a generated library");

    /// <summary>Works that have an EPUB and a PDF, as the hashes of one file of each.</summary>
    private List<(string Epub, string Pdf)> Pairs() =>
        [.. Truth().Files
            .Where(f => f.DuplicateOf is null)
            .GroupBy(f => f.Work)
            .Select(g => (Epub: g.FirstOrDefault(f => f.Format == "epub")?.Sha256, Pdf: g.FirstOrDefault(f => f.Format == "pdf")?.Sha256))
            .Where(p => p.Epub is not null && p.Pdf is not null)
            .Select(p => (p.Epub!, p.Pdf!))];

    /// <summary>The work (sidecar ID) that holds each file of the library, by the hash of the file.</summary>
    private Dictionary<string, string> LibraryWorks()
    {
        var works = new Dictionary<string, string>(StringComparer.Ordinal);
        var root = Path.Combine(workspace.WorkingDirectory, Library);
        foreach (var sidecarPath in Directory.EnumerateFiles(root, SidecarStore.FileName, SearchOption.AllDirectories))
        {
            var sidecar = SidecarStore.Read(sidecarPath);
            foreach (var file in sidecar.Files)
            {
                var hash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(sidecarPath)!, file.Name))));
                works[hash] = sidecar.Id.ToString();
            }
        }

        return works;
    }
}
