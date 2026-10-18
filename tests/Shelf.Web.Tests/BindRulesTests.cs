using Xunit;

namespace Shelf.Web.Tests;

public sealed class BindRulesTests
{
    [Theory]
    [InlineData("127.0.0.1", null)]
    [InlineData("127.0.0.2", null)]
    [InlineData("::1", null)]
    [InlineData("0.0.0.0", "token")]
    [InlineData("::", "token")]
    [InlineData("192.168.1.10", "token")]
    public void An_address_is_accepted_when_it_is_loopback_or_has_a_token(string bind, string? token)
    {
        Assert.True(BindRules.TryValidate(bind, token, out var address, out var error));
        Assert.Equal(bind, address.ToString());
        Assert.Equal(string.Empty, error);
    }

    [Theory]
    [InlineData("0.0.0.0", null)]
    [InlineData("0.0.0.0", "")]
    [InlineData("::", null)]
    [InlineData("10.0.0.5", null)]
    public void An_address_that_is_not_loopback_needs_a_token(string bind, string? token)
    {
        Assert.False(BindRules.TryValidate(bind, token, out _, out var error));
        Assert.Contains("requires --token", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("example.com")]
    [InlineData("")]
    [InlineData("127.0.0")]
    [InlineData("127.1")]
    [InlineData("0")]
    [InlineData("2130706433")]
    [InlineData("1.2.3.4.5")]
    public void A_value_that_is_no_ip_address_is_refused(string bind)
    {
        Assert.False(BindRules.TryValidate(bind, "token", out _, out var error));
        Assert.Contains("is not an IP address", error, StringComparison.Ordinal);
    }
}
