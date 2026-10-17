using Shelf.Core.Configuration;
using Xunit;

namespace Shelf.Core.Tests.Configuration;

public sealed class ConfigTemplateTests
{
    [Fact]
    public void The_library_template_sets_nothing() =>
        Assert.Empty(ConfigFile.Parse(ConfigTemplate.Library, "template").LeafKeys());

    [Fact]
    public void Uncommenting_the_template_gives_the_built_in_defaults()
    {
        var uncommented = string.Join(
            '\n',
            ConfigTemplate.Library.Split('\n').Select(line => line.StartsWith("# [", StringComparison.Ordinal) || line.StartsWith("# ", StringComparison.Ordinal) && line.Contains(" = ", StringComparison.Ordinal)
                ? line[2..]
                : line));

        var settings = ShelfSettings.Resolve(null, ConfigFile.Parse(uncommented, "template"));

        Assert.Equal(GroupingSettings.Default, settings.Grouping);
        Assert.Empty(settings.Warnings);
        Assert.Equal(4, ConfigFile.Parse(uncommented, "template").LeafKeys().Count);
    }
}
