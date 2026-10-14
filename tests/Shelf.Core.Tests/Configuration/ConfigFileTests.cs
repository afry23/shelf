using Shelf.Core.Configuration;
using Xunit;

namespace Shelf.Core.Tests.Configuration;

public sealed class ConfigFileTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "shelf-core-tests", Guid.NewGuid().ToString("N"));

    public ConfigFileTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void Invalid_utf8_is_an_error_without_line()
    {
        var path = Path.Combine(_directory, "config.toml");
        File.WriteAllBytes(path, [(byte)'a', (byte)' ', (byte)'=', (byte)' ', (byte)'"', 0xFF, 0xFE, (byte)'"']);

        var e = Assert.Throws<ConfigException>(() => ConfigFile.Load(path));

        Assert.Null(e.Line);
        Assert.Null(e.Column);
        Assert.Equal(path, e.SourceName);
        Assert.Contains("UTF-8", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_directory_instead_of_a_file_is_an_error()
    {
        var e = Assert.Throws<ConfigException>(() => ConfigFile.Load(_directory));

        Assert.Null(e.Line);
        Assert.StartsWith(_directory, e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_parent_directory_is_an_empty_configuration()
    {
        var path = Path.Combine(_directory, "no-such-dir", "config.toml");

        var document = ConfigFile.Load(path);

        Assert.Equal(path, document.SourceName);
        Assert.False(document.Contains("a"));
    }

    [Fact]
    public void A_missing_file_is_an_empty_configuration()
    {
        var path = Path.Combine(_directory, "config.toml");

        var document = ConfigFile.Load(path);

        Assert.Equal(path, document.SourceName);
        Assert.False(document.Contains("a"));
    }

    [Fact]
    public void Syntax_error_has_one_based_line_and_formatted_message()
    {
        var path = Path.Combine(_directory, "config.toml");
        File.WriteAllText(path, "a = 1\nb = 2\nc =\n");

        var e = Assert.Throws<ConfigException>(() => ConfigFile.Load(path));

        Assert.Equal(3, e.Line);
        Assert.Equal(path, e.SourceName);
        // Fallback format without column: <source>:<line>: <message> (the library reports no column).
        Assert.StartsWith($"{path}:3: ", e.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("   at ", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_uses_the_source_name_in_errors()
    {
        var e = Assert.Throws<ConfigException>(() => ConfigFile.Parse("a =\n", "inline"));

        Assert.Equal("inline", e.SourceName);
        Assert.StartsWith("inline:1: ", e.Message, StringComparison.Ordinal);
        Assert.Equal(1, e.Line);
    }

    [Fact]
    public void Duplicate_key_is_reported_at_the_second_definition()
    {
        var e = Assert.Throws<ConfigException>(() => ConfigFile.Parse("[g]\nx = 1\nx = 2\n", "t"));

        Assert.Equal(3, e.Line);
    }
}
