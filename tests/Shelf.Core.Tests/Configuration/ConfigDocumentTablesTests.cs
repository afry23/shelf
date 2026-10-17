using Shelf.Core.Configuration;
using Xunit;

namespace Shelf.Core.Tests.Configuration;

public sealed class ConfigDocumentTablesTests
{
    [Fact]
    public void LeafKeys_lists_non_table_values_with_dotted_keys_in_stable_order()
    {
        var document = ConfigFile.Parse("b = 1\n[grouping]\nreview = 0.5\n[grouping.deep]\nx = true\n[[libraries]]\nname = 'a'\n", "t.toml");

        Assert.Equal(["b", "grouping.deep.x", "grouping.review", "libraries"], document.LeafKeys());
    }

    [Fact]
    public void GetTables_returns_the_elements_of_an_array_of_tables()
    {
        var document = ConfigFile.Parse("[[libraries]]\nname = 'a'\n[[libraries]]\nname = 'b'\n", "t.toml");

        var tables = document.GetTables("libraries");

        Assert.Equal(["a", "b"], tables.Select(t => t.GetString("name")));
    }

    [Fact]
    public void GetTables_is_empty_when_the_key_is_absent() =>
        Assert.Empty(ConfigDocument.Empty.GetTables("libraries"));

    [Fact]
    public void GetTables_rejects_an_array_of_scalars()
    {
        var document = ConfigFile.Parse("libraries = [1, 2]\n", "t.toml");

        var error = Assert.Throws<ConfigException>(() => document.GetTables("libraries"));

        Assert.Equal("t.toml: libraries[1]: expected table, found integer", error.Message);
    }

    [Fact]
    public void Errors_inside_an_element_name_the_element()
    {
        var document = ConfigFile.Parse("[[libraries]]\nname = 'a'\n[[libraries]]\nname = 3\n", "t.toml");

        var error = Assert.Throws<ConfigException>(() => document.GetTables("libraries")[1].GetString("name"));

        Assert.Equal("libraries[2].name", error.Key);
    }
}
