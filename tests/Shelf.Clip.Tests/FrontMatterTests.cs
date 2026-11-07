using Xunit;

namespace Shelf.Clip.Tests;

public sealed class FrontMatterTests
{
    [Fact]
    public void Without_front_matter_the_text_is_the_body()
    {
        var (fields, body) = FrontMatter.Split("# Title\n\nText\n");
        Assert.Empty(fields);
        Assert.Equal("# Title\n\nText\n", body);
    }

    [Fact]
    public void An_unclosed_front_matter_is_none()
    {
        var (fields, body) = FrontMatter.Split("---\ntitle: x\nText\n");
        Assert.Empty(fields);
        Assert.StartsWith("---", body, StringComparison.Ordinal);
    }

    [Fact]
    public void Scalars_inline_lists_and_block_lists_are_read()
    {
        var (fields, body) = FrontMatter.Split("---\r\ntitle: \"A: B\"\r\nauthor:\r\n  - \"[[Jane Doe]]\"\r\n  - John Roe\r\ntags: [a, \"b, c\"]\r\n---\r\nBody");
        Assert.Equal(["A: B"], fields["title"]);
        Assert.Equal(["Jane Doe", "John Roe"], fields["author"]);
        Assert.Equal(["a", "b, c"], fields["TAGS"]);
        Assert.Equal("Body", body);
    }

    [Theory]
    [InlineData("\"quoted\"", "quoted")]
    [InlineData("'it''s'", "it's")]
    [InlineData("[[Jane Doe]]", "Jane Doe")]
    [InlineData("[[people/jane|Jane Doe]]", "Jane Doe")]
    [InlineData("  plain  ", "plain")]
    public void Unquote_removes_quotes_and_wikilink_brackets(string value, string expected) =>
        Assert.Equal(expected, FrontMatter.Unquote(value));
}
