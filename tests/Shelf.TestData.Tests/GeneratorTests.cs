using Shelf.TestData.Model;
using Xunit;

namespace Shelf.TestData.Tests;

public sealed class GeneratorTests
{
    [Fact]
    public void GroundTruthCarriesOnlyKnownTraitsAndConsistentRelations()
    {
        var directory = Path.Combine(Path.GetTempPath(), "shelf-testdata-tests", Guid.NewGuid().ToString("N"));
        try
        {
            LibraryGenerator.Generate(new GeneratorOptions(200, 300, 42), directory);
            var truth = GroundTruth.Load(Path.Combine(directory, "ground-truth.json"));
            Assert.All(truth.Files.SelectMany(f => f.Traits), t => Assert.Contains(t, Traits.All));
            Assert.All(truth.Files.Where(f => f.Format == "pdf"), f => Assert.True(f.PageCount >= 1));
            Assert.All(truth.Files.Where(f => f.Format == "epub"), f => Assert.NotNull(f.EpubVersion));
            var keys = truth.Works.Select(w => w.Key).ToHashSet(StringComparer.Ordinal);
            Assert.All(truth.Relations, r => Assert.True(keys.Contains(r.A) && keys.Contains(r.B) && r.A != r.B));
            Assert.All(truth.Works.Where(w => w.Isbn13 is not null), w => Assert.True(Content.Isbn.IsValid(w.Isbn13!)));
            Assert.Equal(truth.Files.Sum(f => f.Size), truth.Summary.TotalBytes);
            Assert.Equal(truth.Works.Select(w => w.Isbn13).Where(i => i is not null).Count(), truth.Works.Select(w => w.Isbn13).Where(i => i is not null).Distinct().Count());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void EditionPairsShareTitleAndAuthorsButNotIsbn()
    {
        var directory = Path.Combine(Path.GetTempPath(), "shelf-testdata-tests", Guid.NewGuid().ToString("N"));
        try
        {
            LibraryGenerator.Generate(new GeneratorOptions(200, 300, 42), directory);
            var truth = GroundTruth.Load(Path.Combine(directory, "ground-truth.json"));
            var works = truth.Works.ToDictionary(w => w.Key, StringComparer.Ordinal);
            var editions = truth.Relations.Where(r => r.Kind == RelationKinds.Edition).ToList();
            Assert.Equal(4, editions.Count);
            foreach (var relation in editions)
            {
                var a = works[relation.A];
                var b = works[relation.B];
                Assert.Equal(a.Title, b.Title);
                Assert.Equal(a.Authors, b.Authors);
                Assert.NotEqual(a.Isbn13, b.Isbn13);
                Assert.InRange(b.Year - a.Year, 2, 6);
            }

            foreach (var relation in truth.Relations.Where(r => r.Kind == RelationKinds.SameTitleDifferentAuthor))
            {
                var a = works[relation.A];
                var b = works[relation.B];
                Assert.Equal(a.Title, b.Title);
                Assert.Empty(a.Authors.Select(x => x.Split(' ')[^1]).Intersect(b.Authors.Select(x => x.Split(' ')[^1])));
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact(Explicit = true)]
    public void ReferenceRun()
    {
        var directory = Path.Combine(Path.GetTempPath(), "shelf-testdata-reference", Guid.NewGuid().ToString("N"));
        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = LibraryGenerator.Generate(new GeneratorOptions(10_000, 15_000, 42), directory);
            stopwatch.Stop();
            var truth = GroundTruth.Load(Path.Combine(directory, "ground-truth.json"));
            Assert.Equal(10_000, truth.Works.Count);
            Assert.Equal(15_000, truth.Files.Count);
            Assert.Equal((5500, 4000, 500), (truth.Summary.SingleFileWorks, truth.Summary.TwoFileWorks, truth.Summary.ThreeFileWorks));
            Assert.Equal((8300, 6700), (truth.Summary.EpubFiles, truth.Summary.PdfFiles));
            Assert.Equal((250, 250), (truth.Summary.ExactDuplicates, truth.Summary.SameFormatDuplicates));
            Assert.Equal((200, 100, 1000), (truth.Summary.EditionPairs, truth.Summary.HardNegativePairs, truth.Summary.SeriesWorks));
            TestContext.Current.SendDiagnosticMessage($"Reference run: {stopwatch.Elapsed.TotalSeconds:F1} s, {result.TotalBytes} bytes, {result.Files} files");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
