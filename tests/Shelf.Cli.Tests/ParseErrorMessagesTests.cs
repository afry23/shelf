using Xunit;

namespace Shelf.Cli.Tests;

public sealed class ParseErrorMessagesTests
{
    [Theory]
    [InlineData("--x", "Unknown option '--x'")]
    [InlineData("x", "Unknown command 'x'")]
    [InlineData("-x", "Unknown option '-x'")]
    public void Describe_names_the_first_unmatched_token(string token, string expected) =>
        Assert.Equal(expected, ParseErrorMessages.Describe([token, "later"]));

    [Fact]
    public void Describe_returns_null_without_unmatched_tokens() =>
        Assert.Null(ParseErrorMessages.Describe([]));
}
