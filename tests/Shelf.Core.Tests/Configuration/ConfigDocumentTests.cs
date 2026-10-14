using System.Globalization;
using Shelf.Core.Configuration;
using Xunit;

namespace Shelf.Core.Tests.Configuration;

public sealed class ConfigDocumentTests
{
    private static ConfigDocument Parse(string toml) => ConfigFile.Parse(toml, "test.toml");

    [Theory]
    [InlineData("[grouping]\nauto_merge = 0.9")]
    [InlineData("grouping.auto_merge = 0.9")]
    [InlineData("grouping = { auto_merge = 0.9 }")]
    [InlineData("[grouping.nested]\n[grouping]\nauto_merge = 0.9")]
    public void Header_dotted_key_and_inline_table_give_the_same_value(string toml)
    {
        Assert.Equal(0.9, Parse(toml).GetFloat("grouping.auto_merge"));
    }

    [Fact]
    public void Descending_into_a_scalar_is_absent()
    {
        var document = Parse("title = \"x\"\n");

        Assert.False(document.Contains("title.sub"));
        Assert.Null(document.GetString("title.sub"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("a..b")]
    [InlineData(".a")]
    [InlineData("a.")]
    public void Malformed_keys_are_rejected(string key)
    {
        Assert.Throws<ArgumentException>(() => ConfigDocument.Empty.Contains(key));
    }

    [Fact]
    public void Null_key_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => ConfigDocument.Empty.GetString(null!));
    }

    [Fact]
    public void A_quoted_key_containing_a_dot_is_not_addressable()
    {
        // Documented limitation: each dotted segment is one table key.
        var document = Parse("\"a.b\" = 1\n");

        Assert.False(document.Contains("a.b"));
        Assert.False(document.Contains("a"));
    }

    [Fact]
    public void Empty_document_has_no_values()
    {
        Assert.False(ConfigDocument.Empty.Contains("a"));
        Assert.Null(ConfigDocument.Empty.GetFloat("a"));
    }

    [Fact]
    public void Integer_is_widened_to_float()
    {
        var document = Parse("a = 1\nb = 9223372036854775807\n");

        Assert.Equal(1.0, document.GetFloat("a"));
        Assert.Equal(9223372036854775807L, document.GetInteger("b"));
        Assert.Equal((double)long.MaxValue, document.GetFloat("b"));
    }

    [Fact]
    public void Float_is_not_an_integer()
    {
        var document = Parse("a = 1.0\n");

        var e = Assert.Throws<ConfigException>(() => document.GetInteger("a"));

        Assert.Equal("test.toml: a: expected integer, found float", e.Message);
        Assert.Equal("a", e.Key);
        Assert.Null(e.Line);
    }

    [Theory]
    [InlineData("s = \"x\"", "s", "float", "string")]
    [InlineData("b = true", "b", "integer", "boolean")]
    [InlineData("i = 1", "i", "string", "integer")]
    [InlineData("[t]\nx = 1", "t", "string", "table")]
    [InlineData("a = [1]", "a", "boolean", "array")]
    [InlineData("d = 1979-05-27", "d", "string", "datetime")]
    public void Mismatch_message_names_key_and_both_kinds(string toml, string key, string requested, string found)
    {
        var document = Parse(toml);

        var e = Assert.Throws<ConfigException>(() => Read(document, key, requested));

        Assert.Equal($"test.toml: {key}: expected {requested}, found {found}", e.Message);
    }

    [Fact]
    public void Culture_does_not_change_number_reading_or_message_text()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var document = Parse("a = 0.9\nb = \"x\"\n");

            Assert.Equal(0.9, document.GetFloat("a"));
            var e = Assert.Throws<ConfigException>(() => document.GetFloat("b"));
            Assert.Equal("test.toml: b: expected float, found string", e.Message);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    private static object? Read(ConfigDocument document, string key, string requested) => requested switch
    {
        "string" => document.GetString(key),
        "integer" => document.GetInteger(key),
        "float" => document.GetFloat(key),
        "boolean" => document.GetBoolean(key),
        _ => throw new ArgumentException(requested, nameof(requested)),
    };
}
