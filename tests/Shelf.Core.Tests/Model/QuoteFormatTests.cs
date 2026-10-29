using Shelf.Core.Model;
using Xunit;

namespace Shelf.Core.Tests.Model;

public sealed class QuoteFormatTests
{
    [Fact]
    public void A_quote_over_several_lines_is_one_block_quote_with_the_blank_line_kept()
    {
        var text = QuoteFormat.Render("key2020", "First line\r\n  second   line\n\nthird", "page=3");

        Assert.Equal("> First line\n> second line\n>\n> third\n\n[@key2020, p. 3]", text);
    }

    [Fact]
    public void Empty_lines_around_the_quote_are_dropped()
    {
        Assert.Equal("> x\n\n[@key2020]", QuoteFormat.Render("key2020", "\n\n x \n\n", null));
    }

    [Theory]
    [InlineData("page=12", "[@key2020, p. 12]")]
    [InlineData("epubcfi(/6/6[ch2]!/4/2)", "[@key2020, sec. 3]")]
    [InlineData("epubcfi(/4/2)", "[@key2020]")]
    [InlineData("somewhere", "[@key2020]")]
    [InlineData(null, "[@key2020]")]
    public void The_place_is_a_page_or_a_section_or_left_out(string? locator, string citation) =>
        Assert.EndsWith(citation, QuoteFormat.Render("key2020", "x", locator), StringComparison.Ordinal);

    [Theory]
    [InlineData("page=12", "page 12")]
    [InlineData("epubcfi(/6/4!)", "section 2")]
    [InlineData("somewhere", null)]
    [InlineData(null, null)]
    public void The_place_for_people(string? locator, string? expected) => Assert.Equal(expected, QuoteFormat.Place(locator));

    [Theory]
    [InlineData("", "x")]
    [InlineData("key", "  ")]
    public void A_cite_key_and_a_quote_are_needed(string key, string quote) =>
        Assert.Throws<ArgumentException>(() => QuoteFormat.Render(key, quote, null));
}
