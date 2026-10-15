using Shelf.TestData.Planning;
using Xunit;

namespace Shelf.TestData.Tests;

public sealed class PlannerTests
{
    // works, files, single, double, triple, epub, pdf, exact, same-format, editions, hard negatives, series
    [Theory]
    [InlineData(10, 10, 10, 0, 0, 6, 4, 0, 0, 0, 0, 1)]
    [InlineData(10, 15, 5, 5, 0, 8, 7, 0, 0, 0, 0, 1)]
    [InlineData(10, 21, 0, 9, 1, 11, 10, 1, 0, 0, 0, 1)]
    [InlineData(20, 30, 11, 8, 1, 17, 13, 1, 0, 0, 0, 2)]
    [InlineData(20, 40, 2, 16, 2, 22, 18, 1, 1, 0, 0, 2)]
    [InlineData(100, 150, 55, 40, 5, 83, 67, 3, 2, 2, 1, 10)]
    [InlineData(200, 300, 110, 80, 10, 166, 134, 5, 5, 4, 2, 20)]
    [InlineData(10000, 15000, 5500, 4000, 500, 8300, 6700, 250, 250, 200, 100, 1000)]
    public void PlanArithmetic(int works, int files, int single, int twoFile, int threeFile, int epub, int pdf, int exact, int sameFormat, int editions, int hardNegatives, int series)
    {
        var plan = LibraryPlanner.Plan(works, files);
        Assert.Equal((single, twoFile, threeFile), (plan.SingleFileWorks, plan.TwoFileWorks, plan.ThreeFileWorks));
        Assert.Equal((epub, pdf), (plan.EpubFiles, plan.PdfFiles));
        Assert.Equal((exact, sameFormat), (plan.ExactDuplicates, plan.SameFormatDuplicates));
        Assert.Equal((editions, hardNegatives, series), (plan.EditionPairs, plan.HardNegativePairs, plan.SeriesWorks));
        Assert.Equal(files, plan.EpubFiles + plan.PdfFiles);
    }

    [Fact]
    public void MaxFilesForTenWorksIsTwentyOne() => Assert.Equal(21, LibraryPlanner.MaxFiles(10));

    [Theory]
    [InlineData(10, 22, "--files must be between 10 and 21 for 10 works")]
    [InlineData(10, 9, "--files must be between 10 and 21 for 10 works")]
    [InlineData(0, 0, "--works must be at least 1")]
    public void InvalidShapesAreUsageErrors(int works, int files, string message) =>
        Assert.Equal(message, Assert.Throws<UsageException>(() => LibraryPlanner.Plan(works, files)).Message);
}
