using Xunit;

namespace Shelf.Specs.Steps;

public sealed class ArgumentSplitterTests
{
    [Theory]
    [InlineData("", new string[0])]
    [InlineData("   ", new string[0])]
    [InlineData("--version", new[] { "--version" })]
    [InlineData("search  foo   --json", new[] { "search", "foo", "--json" })]
    [InlineData("search \"event sourcing\" --json", new[] { "search", "event sourcing", "--json" })]
    [InlineData("init \"/tmp/my library\"", new[] { "init", "/tmp/my library" })]
    [InlineData("edit \"\" x", new[] { "edit", "", "x" })]
    [InlineData("a\"b c\"d", new[] { "ab cd" })]
    public void Split_separates_on_whitespace_and_groups_quoted_text(string text, string[] expected) =>
        Assert.Equal(expected, ArgumentSplitter.Split(text));

    [Fact]
    public void Split_rejects_an_unterminated_quote() =>
        Assert.Throws<ArgumentException>(() => ArgumentSplitter.Split("search \"oops"));
}
