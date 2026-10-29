using Shelf.Core.Model;
using Xunit;

namespace Shelf.Core.Tests.Model;

public sealed class BibTeXTests
{
    [Theory]
    [InlineData(@"M{\""u}ller", "Müller")]
    [InlineData(@"Fran\c{c}ois", "François")]
    [InlineData(@"\'{\i}", "í")]
    [InlineData(@"Stra\ss e", "Straße")]
    [InlineData(@"{\O}rsted", "Ørsted")]
    [InlineData(@"AT\&T \% \_x", "AT&T % _x")]
    [InlineData("Smith~et~al.", "Smith et al.")]
    [InlineData("  a\n   b  ", "a b")]
    public void Clean_reads_latex(string raw, string expected)
    {
        Assert.Equal(expected, BibTeX.Clean(raw));
    }

    [Fact]
    public void SplitNames_handles_both_name_orders_and_braced_and()
    {
        var names = BibTeX.SplitNames("Knuth, Donald E. and Leslie Lamport AND {Barnes and Noble} and Smith, Jr., John and others");
        Assert.Equal(["Donald E. Knuth", "Leslie Lamport", "Barnes and Noble", "John Smith Jr."], names);
    }

    [Fact]
    public void Parse_joins_macros_and_skips_comments()
    {
        var entries = BibTeX.Parse("""
            @string{a = "Alpha"}
            @comment{@article{no, title={no}}}
            @preamble{"\newcommand{\x}{y}"}
            Text between entries.
            @Article(k1, Title = a # " and " # {Beta}, year = 1999, )
            """);
        var entry = Assert.Single(entries);
        Assert.Equal("article", entry.Type);
        Assert.Equal("k1", entry.Key);
        Assert.Equal("Alpha and Beta", entry.Text("title"));
        Assert.Equal("1999", entry.Text("year"));
    }

    [Theory]
    [InlineData("@article{k1,")]
    [InlineData("@article{k1 title={x}}")]
    [InlineData("@article{,title={x}}")]
    [InlineData("nothing")]
    public void Parse_drops_what_is_not_an_entry(string text) => Assert.Empty(BibTeX.Parse(text));

    [Fact]
    public void Parse_keeps_entries_before_an_unclosed_one()
    {
        var entries = BibTeX.Parse("@article{a, title={A}}\n@article{b, title={B");
        Assert.Equal("a", Assert.Single(entries).Key);
    }
}
