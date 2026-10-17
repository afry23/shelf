using System.Text.Json;
using Shelf.Core.Configuration;
using Shelf.Core.Libraries;
using Xunit;

namespace Shelf.Core.Tests.Libraries;

public sealed class LibraryInitializerTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("shelf-init-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void A_relative_root_is_a_programming_error() =>
        Assert.Throws<ArgumentException>(() => LibraryInitializer.Initialize("relative"));

    [Fact]
    public void The_review_queue_of_a_new_library_is_valid_JSON_without_entries()
    {
        var root = Path.Combine(_directory, "lib");

        LibraryInitializer.Initialize(root);

        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, ".shelf", "review.json")));
        Assert.Equal(1, document.RootElement.GetProperty("schema").GetInt32());
        Assert.Equal(0, document.RootElement.GetProperty("entries").GetArrayLength());
    }

    [Fact]
    public void The_config_of_a_new_library_is_the_template_and_changes_no_setting()
    {
        var root = Path.Combine(_directory, "lib");

        LibraryInitializer.Initialize(root);

        var settings = ShelfSettings.Resolve(null, ConfigFile.Load(ConfigPaths.Library(root)));
        Assert.Equal(GroupingSettings.Default, settings.Grouping);
        Assert.Empty(settings.Warnings);
    }

    [Fact]
    public void A_trailing_separator_is_accepted()
    {
        LibraryInitializer.Initialize(Path.Combine(_directory, "lib") + Path.DirectorySeparatorChar);

        Assert.True(Directory.Exists(Path.Combine(_directory, "lib", ".shelf")));
    }

    [Fact]
    public void Nothing_is_left_behind_when_the_folder_is_inside_a_library()
    {
        LibraryInitializer.Initialize(Path.Combine(_directory, "lib"));

        Assert.Throws<LibraryInitException>(() => LibraryInitializer.Initialize(Path.Combine(_directory, "lib", "a", "b")));

        Assert.False(Directory.Exists(Path.Combine(_directory, "lib", "a")));
    }
}
