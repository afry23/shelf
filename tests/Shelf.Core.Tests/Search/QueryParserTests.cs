using Shelf.Core.Search;
using Xunit;

namespace Shelf.Core.Tests.Search;

public sealed class QueryParserTests
{
    [Fact]
    public void A_term_knows_whether_it_was_quoted_or_a_prefix()
    {
        var query = QueryParser.Parse("plain \"two words\" pre*");

        Assert.Equal(
            [new SearchTerm("plain", false, false), new SearchTerm("two words", true, false), new SearchTerm("pre", false, true)],
            query.Include);
    }

    [Fact]
    public void A_star_inside_quotes_is_not_a_prefix() =>
        Assert.Equal([new SearchTerm("pre*", true, false)], QueryParser.Parse("\"pre*\"").Include);

    [Theory]
    [InlineData("tag:a -tag:b", 2)]
    [InlineData("publisher:manning -publisher:x", 2)]
    [InlineData("year:2015 -year:2016", 2)]
    public void Filters_keep_their_order_and_negation(string text, int count)
    {
        var filters = QueryParser.Parse(text).Filters;

        Assert.Equal(count, filters.Count);
        Assert.False(filters[0].Negated);
        Assert.True(filters[1].Negated);
    }

    [Fact]
    public void A_filter_value_in_quotes_can_hold_spaces_and_the_rest_of_the_query_goes_on()
    {
        var query = QueryParser.Parse("author:\"Martin Kleppmann\" data");

        Assert.Equal(new TextFilter(FieldKind.Author, new SearchTerm("Martin Kleppmann", true, false), false), Assert.Single(query.Filters));
        Assert.Equal([new SearchTerm("data", false, false)], query.Include);
    }

    [Fact]
    public void An_unfinished_filter_quote_runs_to_the_end()
    {
        var query = QueryParser.Parse("title:\"open end");

        Assert.Equal(new TextFilter(FieldKind.Title, new SearchTerm("open end", true, false), false), Assert.Single(query.Filters));
    }

    [Theory]
    [InlineData("year:0", 0, 0)]
    [InlineData("year:9999", 9999, 9999)]
    [InlineData("year:>9999", 10000, null)]
    [InlineData("year:<1", null, 0)]
    public void Year_edges(string text, int? min, int? max)
    {
        var filter = Assert.IsType<YearFilter>(Assert.Single(QueryParser.Parse(text).Filters));

        Assert.Equal(min, filter.Min);
        Assert.Equal(max, filter.Max);
    }

    [Theory]
    [InlineData("year:")]
    [InlineData("year:>=")]
    [InlineData("year:2015..")]
    [InlineData("year:..2015")]
    [InlineData("year:2015...2016")]
    [InlineData("year:-5")]
    public void Malformed_years_are_errors(string text) => Assert.Throws<QueryException>(() => QueryParser.Parse(text));

    [Fact]
    public void A_word_with_a_colon_that_is_no_field_is_plain_text()
    {
        Assert.Equal([new SearchTerm("http://example.org/x", false, false)], QueryParser.Parse("http://example.org/x").Include);
        Assert.Equal([new SearchTerm(":leading", false, false)], QueryParser.Parse(":leading").Include);
    }

    [Fact]
    public void Characters_of_the_index_syntax_stay_in_the_terms()
    {
        var query = QueryParser.Parse("NEAR(a b) a:b ^x OR AND");

        Assert.Equal(
            ["NEAR(a", "b)", "a:b", "^x", "OR", "AND"],
            query.Include.Select(t => t.Text));
    }

    [Fact]
    public void Null_is_rejected() => Assert.Throws<ArgumentNullException>(() => QueryParser.Parse(null!));

    [Fact]
    public void Describe_of_a_query_with_everything()
    {
        var query = QueryParser.Parse("kube* \"a b\" -x tag:t lang:EN year:2010..2012 publisher:\"O'Reilly\"");

        Assert.Equal("include: kube*, \"a b\"; exclude: x; filters: tag:t, lang:en, year:2010..2012, publisher:\"O'Reilly\"", query.Describe());
    }
}
