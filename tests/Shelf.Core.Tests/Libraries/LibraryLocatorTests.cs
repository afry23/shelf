using Shelf.Core.Configuration;
using Shelf.Core.Libraries;
using Xunit;

namespace Shelf.Core.Tests.Libraries;

public sealed class LibraryLocatorTests
{
    private static ShelfSettings Settings(string toml) =>
        ShelfSettings.Resolve(ConfigFile.Parse(toml, "global.toml"), library: null);

    [Fact]
    public void A_library_at_the_file_system_root_is_found_from_a_nested_directory()
    {
        var result = LibraryLocator.Resolve(null, "/a/b", ShelfSettings.Defaults, d => d == "/.shelf");

        Assert.Equal(new LibraryResolution("/", LibrarySource.WorkingDirectory), result);
    }

    [Fact]
    public void The_nearest_library_wins_over_an_outer_one()
    {
        var result = LibraryLocator.Resolve(null, "/outer/inner/x", ShelfSettings.Defaults,
            d => d is "/outer/.shelf" or "/outer/inner/.shelf");

        Assert.Equal("/outer/inner", result.Root);
    }

    [Fact]
    public void A_configured_name_wins_over_a_relative_path_of_the_same_spelling()
    {
        var settings = Settings("[[libraries]]\nname = 'work'\npath = '/books/work'\n");

        var result = LibraryLocator.Resolve("work", "/tmp", settings, d => d is "/books/work/.shelf" or "/tmp/work/.shelf");

        Assert.Equal("/books/work", result.Root);
    }

    [Fact]
    public void A_default_library_without_a_marker_folder_is_an_error()
    {
        var settings = Settings("default_library = 'main'\n[[libraries]]\nname = 'main'\npath = '/books/main'\n");

        var error = Assert.Throws<LibraryNotFoundException>(() => LibraryLocator.Resolve(null, "/tmp", settings, _ => false));

        Assert.Contains("default library 'main'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Without_any_candidate_the_error_says_what_to_do()
    {
        var error = Assert.Throws<LibraryNotFoundException>(() =>
            LibraryLocator.Resolve(null, "/tmp", ShelfSettings.Defaults, _ => false));

        Assert.Contains("--library", error.Message, StringComparison.Ordinal);
    }
}
