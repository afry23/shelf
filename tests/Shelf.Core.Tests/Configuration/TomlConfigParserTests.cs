using System.Text;
using Shelf.Core.Configuration;
using Xunit;

namespace Shelf.Core.Tests.Configuration;

public sealed class TomlConfigParserTests
{
    private static ConfigDocument Parse(string toml) => ConfigFile.Parse(toml, "test.toml");

    [Theory]
    [InlineData("v = \"basic\"", ConfigValueKind.String)]
    [InlineData("v = 'literal'", ConfigValueKind.String)]
    [InlineData("v = \"\"\"\nmulti\nline\"\"\"", ConfigValueKind.String)]
    [InlineData("v = '''\nmulti\nliteral'''", ConfigValueKind.String)]
    [InlineData("v = 42", ConfigValueKind.Integer)]
    [InlineData("v = -17", ConfigValueKind.Integer)]
    [InlineData("v = 0xDEAD_BEEF", ConfigValueKind.Integer)]
    [InlineData("v = 0o755", ConfigValueKind.Integer)]
    [InlineData("v = 0b1101", ConfigValueKind.Integer)]
    [InlineData("v = 3.14", ConfigValueKind.Float)]
    [InlineData("v = 5e+22", ConfigValueKind.Float)]
    [InlineData("v = inf", ConfigValueKind.Float)]
    [InlineData("v = -inf", ConfigValueKind.Float)]
    [InlineData("v = nan", ConfigValueKind.Float)]
    [InlineData("v = true", ConfigValueKind.Boolean)]
    [InlineData("v = false", ConfigValueKind.Boolean)]
    [InlineData("v = 1979-05-27T07:32:00Z", ConfigValueKind.DateTime)]
    [InlineData("v = 1979-05-27T07:32:00-07:00", ConfigValueKind.DateTime)]
    [InlineData("v = 1979-05-27T07:32:00", ConfigValueKind.DateTime)]
    [InlineData("v = 1979-05-27", ConfigValueKind.DateTime)]
    [InlineData("v = 07:32:00", ConfigValueKind.DateTime)]
    [InlineData("v = [1, 2, 3]", ConfigValueKind.Array)]
    [InlineData("v = [1, \"two\", 3.0]", ConfigValueKind.Array)]
    [InlineData("v = { a = 1 }", ConfigValueKind.Table)]
    [InlineData("[v]\na = 1", ConfigValueKind.Table)]
    [InlineData("[[v]]\na = 1\n[[v]]\na = 2", ConfigValueKind.Array)]
    public void Every_toml_type_maps_to_the_right_kind(string toml, ConfigValueKind expectedKind)
    {
        var document = Parse(toml);

        Assert.Equal(expectedKind, document.GetKind("v"));
    }

    [Theory]
    [InlineData("v = 1979-05-27T07:32:00Z", "1979-05-27T07:32:00.0000000+00:00")]
    [InlineData("v = 1979-05-27T07:32:00-07:00", "1979-05-27T07:32:00.0000000-07:00")]
    [InlineData("v = 1979-05-27T07:32:00", "1979-05-27T07:32:00")]
    [InlineData("v = 1979-05-27", "1979-05-27")]
    [InlineData("v = 07:32:00", "07:32:00")]
    [InlineData("v = 07:32:00.5", "07:32:00.5")]
    public void Datetimes_are_kept_as_iso_8601_text(string toml, string expected)
    {
        var document = Parse(toml);

        Assert.Equal(expected, document.Root["v"].Value);
    }

    [Fact]
    public void Library_exception_types_all_derive_from_the_base_the_adapter_catches()
    {
        Assert.True(typeof(CsToml.Error.CsTomlSerializeException).IsAssignableTo(typeof(CsToml.Error.CsTomlException)));
    }

    [Fact]
    public void String_forms_yield_their_text()
    {
        var document = Parse("a = \"caf\\u00e9\"\nb = 'C:\\path'\nc = \"\"\"\nline one\nline two\"\"\"\n");

        Assert.Equal("café", document.GetString("a"));
        Assert.Equal("C:\\path", document.GetString("b"));
        Assert.Equal("line one\nline two", document.GetString("c"));
    }

    [Fact]
    public void Special_floats_are_read()
    {
        var document = Parse("a = inf\nb = -inf\nc = nan\n");

        Assert.Equal(double.PositiveInfinity, document.GetFloat("a"));
        Assert.Equal(double.NegativeInfinity, document.GetFloat("b"));
        Assert.True(double.IsNaN(document.GetFloat("c")!.Value));
    }

    [Fact]
    public void Nested_tables_and_arrays_of_tables_are_addressable_as_tables()
    {
        var document = Parse("[a.b]\nc = 1\n[[items]]\nn = 1\n");

        Assert.Equal(1, document.GetInteger("a.b.c"));
        Assert.Equal(ConfigValueKind.Array, document.GetKind("items"));
    }

    [Fact]
    public void Integer_overflow_is_a_syntax_error_with_a_line()
    {
        var e = Assert.Throws<ConfigException>(() => Parse("a = 1\nn = 9223372036854775808\n"));

        Assert.Equal(2, e.Line);
    }

    [Fact]
    public void Crlf_input_gives_the_same_document_as_lf()
    {
        const string lf = "title = \"x\"\n\n[grouping]\nauto_merge = 0.9\nreview = 5\n";
        var crlf = lf.Replace("\n", "\r\n", StringComparison.Ordinal);

        var a = Parse(lf);
        var b = Parse(crlf);

        Assert.Equal(a.GetString("title"), b.GetString("title"));
        Assert.Equal(a.GetFloat("grouping.auto_merge"), b.GetFloat("grouping.auto_merge"));
        Assert.Equal(a.GetInteger("grouping.review"), b.GetInteger("grouping.review"));
    }

    [Fact]
    public void Utf8_bom_is_accepted_and_ignored()
    {
        // Q12: TOML 1.0 does not mention a BOM. Recorded behavior of CsToml 1.8.6: a leading BOM is skipped.
        var path = Path.Combine(Path.GetTempPath(), "shelf-core-tests-" + Guid.NewGuid().ToString("N") + ".toml");
        try
        {
            File.WriteAllBytes(path, [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes("a = 1\n")]);

            var document = ConfigFile.Load(path);

            Assert.Equal(1, document.GetInteger("a"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("t = { a = 1, }")]
    [InlineData("t = {\n  a = 1\n}")]
    [InlineData("t = 07:32")]
    public void Toml_1_1_only_syntax_is_rejected(string toml)
    {
        // The reader is pinned to TOML 1.0.0 (ADR 0003, Q9).
        Assert.Throws<ConfigException>(() => Parse(toml));
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("\n\n", true)]
    [InlineData("# a\r\n  # b\t\r\n\t\n", true)]
    [InlineData("# a = [1, 2]\n# [t]", true)]
    [InlineData("a = 1", false)]
    [InlineData("# a\na = 1", false)]
    [InlineData("[t]", false)]
    [InlineData("  a = 1 # c", false)]
    [InlineData("# \u00e4", false)]
    [InlineData("# a\u0001", false)]
    [InlineData("# a\u007f", false)]
    public void HasNoContent_is_true_only_for_plain_ASCII_blank_and_comment_lines(string text, bool expected) =>
        Assert.Equal(expected, TomlConfigParser.HasNoContent(Encoding.UTF8.GetBytes(text)));

    [Fact]
    public void A_comment_with_a_byte_order_mark_still_goes_through_the_library()
    {
        var document = ConfigFile.Parse("\uFEFF# nothing\n", "test.toml");

        Assert.False(document.Contains("a"));
    }
}
