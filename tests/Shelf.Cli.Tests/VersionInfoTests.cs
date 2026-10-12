using Xunit;

namespace Shelf.Cli.Tests;

public sealed class VersionInfoTests
{
    [Theory]
    [InlineData("1.2.3+abc123", "1.2.3")]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("1.2.3-beta.1+sha", "1.2.3-beta.1")]
    [InlineData(null, "0.0.0")]
    [InlineData("", "0.0.0")]
    public void Format_strips_the_commit_suffix(string? informationalVersion, string expected) =>
        Assert.Equal(expected, VersionInfo.Format(informationalVersion));
}
