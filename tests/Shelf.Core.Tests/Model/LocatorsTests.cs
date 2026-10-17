using Shelf.Core.Model;
using Xunit;

namespace Shelf.Core.Tests.Model;

public sealed class LocatorsTests
{
    [Fact]
    public void EpubChapter_rejects_invalid_arguments()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Locators.EpubChapter(-1, "x"));
        Assert.Throws<ArgumentException>(() => Locators.EpubChapter(0, string.Empty));
    }

    [Fact]
    public void PdfPage_starts_at_one()
    {
        Assert.Equal("page=1", Locators.PdfPage(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Locators.PdfPage(0));
    }

    [Theory]
    [InlineData("page=3", true, 3)]
    [InlineData("page=0", false, 0)]
    [InlineData("page=+3", false, 0)]
    [InlineData("page=3 ", false, 0)]
    [InlineData("Page=3", false, 0)]
    [InlineData("page=99999999999", false, 0)]
    [InlineData(null, false, 0)]
    public void TryParsePdfPage_accepts_only_plain_positive_numbers(string? locator, bool ok, int page)
    {
        Assert.Equal(ok, Locators.TryParsePdfPage(locator, out var parsed));
        Assert.Equal(page, parsed);
    }
}
