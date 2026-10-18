using Xunit;

namespace Shelf.Cli.Tests;

public sealed class TextFormatTests
{
    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(1024 * 1024 - 1, "1024 KB")]
    [InlineData(5L * 1024 * 1024, "5 MB")]
    [InlineData(3L * 1024 * 1024 * 1024 / 2, "1.5 GB")]
    public void Size_uses_units_of_1024_and_one_decimal(long bytes, string expected) =>
        Assert.Equal(expected, TextFormat.Size(bytes));

    [Theory]
    [InlineData(1, "1 entry")]
    [InlineData(0, "0 entries")]
    [InlineData(2, "2 entries")]
    public void Plural_picks_the_form_by_the_count(int count, string expected) =>
        Assert.Equal(expected, TextFormat.Plural(count, "entry", "entries"));

    [Fact]
    public void Palette_returns_plain_text_when_color_is_off()
    {
        var palette = new Palette(enabled: false);

        Assert.Equal("x", palette.Bold("x"));
        Assert.Equal("x", palette.Dim("x"));
    }

    [Fact]
    public void Palette_wraps_text_in_escape_sequences_when_color_is_on()
    {
        var palette = new Palette(enabled: true);

        Assert.Equal("\e[1mx\e[22m", palette.Bold("x"));
        Assert.Equal("\e[2mx\e[22m", palette.Dim("x"));
    }

    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, false, true)]
    [InlineData(true, true, false, false)]
    [InlineData(true, false, true, false)]
    [InlineData(false, false, true, false)]
    public void Palette_For_needs_a_terminal_no_NO_COLOR_and_no_JSON(bool terminal, bool noColor, bool json, bool colored)
    {
        var environment = CliEnvironment.FromVariables(name => name == "NO_COLOR" && noColor ? "1" : null, terminal);

        Assert.Equal(colored, Palette.For(environment, json).Bold("x") != "x");
    }
}
