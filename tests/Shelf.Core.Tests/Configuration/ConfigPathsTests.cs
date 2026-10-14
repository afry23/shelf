using Shelf.Core.Configuration;
using Xunit;

namespace Shelf.Core.Tests.Configuration;

public sealed class ConfigPathsTests
{
    private static string? None(string name) => null;

    [Theory]
    [InlineData("/home/ada/", "/home/ada/.config/shelf/config.toml")]
    [InlineData("/home/ada", "/home/ada/.config/shelf/config.toml")]
    [InlineData("/", "/.config/shelf/config.toml")]
    public void Home_directory_forms(string home, string expected)
    {
        Assert.Equal(expected, ConfigPaths.Global(None, home));
    }

    [Theory]
    [InlineData("/", "/shelf/config.toml")]
    [InlineData("/xdg//", "/xdg/shelf/config.toml")]
    [InlineData("./relative", "/home/ada/.config/shelf/config.toml")]
    [InlineData("", "/home/ada/.config/shelf/config.toml")]
    public void Xdg_config_home_forms(string value, string expected)
    {
        var path = ConfigPaths.Global(name => name == "XDG_CONFIG_HOME" ? value : null, "/home/ada");

        Assert.Equal(expected, path);
    }

    [Fact]
    public void Only_xdg_config_home_is_consulted()
    {
        var asked = new List<string>();

        ConfigPaths.Global(name => { asked.Add(name); return null; }, "/home/ada");

        Assert.Equal(["XDG_CONFIG_HOME"], asked);
    }

    [Theory]
    [InlineData("/", "/.shelf/config.toml")]
    [InlineData("/books//", "/books/.shelf/config.toml")]
    [InlineData("/books", "/books/.shelf/config.toml")]
    public void Library_root_forms(string root, string expected)
    {
        Assert.Equal(expected, ConfigPaths.Library(root));
    }

    [Fact]
    public void Empty_arguments_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => ConfigPaths.Global(None, ""));
        Assert.Throws<ArgumentException>(() => ConfigPaths.Library(""));
    }
}
