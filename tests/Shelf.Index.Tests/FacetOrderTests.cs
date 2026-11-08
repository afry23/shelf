using Xunit;
using Shelf.Index;

namespace Shelf.Index.Tests;

public sealed class FacetOrderTests
{
    [Fact]
    public void Nothing_is_no_choice()
    {
        Assert.Empty(FacetOrder.Parse(null));
        Assert.Empty(FacetOrder.Parse("  "));
    }

    [Fact]
    public void Several_facets_are_read()
    {
        var orders = FacetOrder.Parse("authors:name.asc, tags:count.desc");

        Assert.Equal(new FacetOrder(false, false), orders["authors"]);
        Assert.Equal(new FacetOrder(true, true), orders["tags"]);
    }

    [Theory]
    [InlineData("authors")]
    [InlineData("authors:name")]
    [InlineData("colors:name.asc")]
    [InlineData("authors:size.asc")]
    [InlineData("authors:name.up")]
    [InlineData("authors:name.asc,authors:count.asc")]
    public void A_bad_choice_is_refused(string text) => Assert.Throws<FormatException>(() => FacetOrder.Parse(text));

    [Theory]
    [InlineData(false, false, false, "lower(x) ASC, x ASC")]
    [InlineData(false, true, false, "lower(x) DESC, x DESC")]
    [InlineData(false, true, true, "x DESC")]
    [InlineData(true, true, false, "2 DESC, x ASC")]
    [InlineData(true, false, true, "2 ASC, x ASC")]
    public void The_order_becomes_sql(bool byCount, bool descending, bool numeric, string expected) =>
        Assert.Equal(expected, new FacetOrder(byCount, descending).Sql("x", numeric));
}
