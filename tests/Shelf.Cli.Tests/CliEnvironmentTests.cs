using Microsoft.Extensions.Logging;
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

    [Fact]
    public void MinimumLogLevel_defaults_to_Warning()
    {
        Assert.Equal(LogLevel.Warning, CliEnvironment.Empty.MinimumLogLevel);
        Assert.Equal(LogLevel.Warning, CliEnvironment.FromVariables(_ => null, stdoutIsTerminal: false).MinimumLogLevel);
    }

    [Theory]
    [InlineData("Trace", LogLevel.Trace)]
    [InlineData("trace", LogLevel.Trace)]
    [InlineData("DEBUG", LogLevel.Debug)]
    [InlineData("None", LogLevel.None)]
    public void SHELF_LOG_LEVEL_selects_the_level_by_name(string value, LogLevel expected)
    {
        var environment = CliEnvironment.FromVariables(n => n == "SHELF_LOG_LEVEL" ? value : null, stdoutIsTerminal: false);

        Assert.Equal(expected, environment.MinimumLogLevel);
        Assert.Null(environment.IgnoredLogLevel);
    }

    [Theory]
    [InlineData("verbose")]
    [InlineData("3")]
    [InlineData("warn")]
    public void SHELF_LOG_LEVEL_with_a_non_name_keeps_the_default_and_is_reported(string value)
    {
        var environment = CliEnvironment.FromVariables(n => n == "SHELF_LOG_LEVEL" ? value : null, stdoutIsTerminal: false);

        Assert.Equal(LogLevel.Warning, environment.MinimumLogLevel);
        Assert.Equal(value, environment.IgnoredLogLevel);
    }

    [Fact]
    public void WithMinimumLogLevel_returns_a_copy_with_the_new_level()
    {
        var original = CliEnvironment.FromVariables(name => name == "NO_COLOR" ? "1" : null, stdoutIsTerminal: true);
        var changed = original.WithMinimumLogLevel(LogLevel.Debug);

        Assert.NotSame(original, changed);
        Assert.Equal(LogLevel.Debug, changed.MinimumLogLevel);
        Assert.True(changed.NoColor);
        Assert.True(changed.StdoutIsTerminal);
    }

    [Fact]
    public void WithMinimumLogLevel_rejects_undefined_values() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => CliEnvironment.Empty.WithMinimumLogLevel((LogLevel)42));
}
