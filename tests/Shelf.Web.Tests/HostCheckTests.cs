using Xunit;

namespace Shelf.Web.Tests;

public sealed class HostCheckTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("127.0.0.1:8420")]
    [InlineData("127.0.0.2:80")]
    [InlineData("localhost")]
    [InlineData("LOCALHOST:8420")]
    [InlineData("[::1]")]
    [InlineData("[::1]:8420")]
    [InlineData("")]
    [InlineData(null)]
    public void A_loopback_host_is_accepted(string? host) => Assert.True(HostCheck.IsLoopbackHost(host));

    [Theory]
    [InlineData("evil.example")]
    [InlineData("evil.example:8420")]
    [InlineData("192.168.1.20")]
    [InlineData("192.168.1.20:8420")]
    [InlineData("localhost.evil.example")]
    [InlineData("127.0.0.1.evil.example")]
    [InlineData("[2001:db8::1]:8420")]
    [InlineData("0.0.0.0")]
    public void Any_other_host_is_refused(string host) => Assert.False(HostCheck.IsLoopbackHost(host));
}
