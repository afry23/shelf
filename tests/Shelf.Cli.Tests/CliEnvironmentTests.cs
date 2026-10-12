using Xunit;

namespace Shelf.Cli.Tests;

public sealed class CliEnvironmentTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("1", true)]
    [InlineData("0", true)]
    [InlineData("false", true)]
    [InlineData("yes", true)]
    public void NoColor_follows_the_NO_COLOR_variable(string? value, bool expected)
    {
        var environment = CliEnvironment.FromVariables(
            name => name == "NO_COLOR" ? value : null,
            stdoutIsTerminal: true);

        Assert.Equal(expected, environment.NoColor);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    public void UseColor_requires_a_terminal_and_no_NO_COLOR(bool noColor, bool terminal, bool expected)
    {
        var environment = CliEnvironment.FromVariables(
            name => name == "NO_COLOR" && noColor ? "1" : null,
            terminal);

        Assert.Equal(expected, environment.UseColor);
    }

    [Fact]
    public void Empty_has_no_color_restriction_and_no_terminal()
    {
        Assert.False(CliEnvironment.Empty.NoColor);
        Assert.False(CliEnvironment.Empty.StdoutIsTerminal);
        Assert.False(CliEnvironment.Empty.UseColor);
    }
}
