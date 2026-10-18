using Xunit;

namespace Shelf.Web.Tests;

public sealed class WithoutTokenTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("?token=abc", "")]
    [InlineData("?token=abc&q=event", "?q=event")]
    [InlineData("?q=event&token=abc&view=list", "?q=event&view=list")]
    [InlineData("?q=a%20b&token=abc", "?q=a%20b")]
    [InlineData("?token=a&token=b", "")]
    [InlineData("?tokens=abc&mytoken=1", "?tokens=abc&mytoken=1")]
    [InlineData("?token", "")]
    public void The_token_parameter_is_dropped_and_the_rest_stays_as_written(string? query, string expected) =>
        Assert.Equal(expected, WebHost.WithoutToken(query));
}
