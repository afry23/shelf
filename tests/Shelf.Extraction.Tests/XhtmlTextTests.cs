using Xunit;

namespace Shelf.Extraction.Tests;

public sealed class XhtmlTextTests
{
    [Fact]
    public void Block_tags_become_line_breaks_and_inline_tags_vanish() =>
        Assert.Equal("\nHello brave\n\n new world\n", XhtmlText.ToPlainText("<p>Hello <b>brave</b></p><p> new world</p>"));

    [Theory]
    [InlineData("<head><title>T</title></head><body>Text</body>", "\nText\n")]
    [InlineData("<style>p { color: red }</style>Text", "Text")]
    [InlineData("<script>if (a < b) { x = '</p>'; }</script>Text", "Text")]
    [InlineData("<svg:script>x</svg:script>Text", "Text")]
    public void Head_script_and_style_are_skipped(string html, string expected) =>
        Assert.Equal(expected, XhtmlText.ToPlainText(html));

    [Theory]
    [InlineData("a&nbsp;b", "a b")]
    [InlineData("&auml;&#228;&#xE4;", "äää")]
    [InlineData("Tom &amp; Jerry", "Tom & Jerry")]
    public void Named_and_numeric_entities_are_decoded(string html, string expected) =>
        Assert.Equal(expected, XhtmlText.ToPlainText(html));

    [Fact]
    public void A_greater_than_sign_inside_an_attribute_does_not_end_the_tag() =>
        Assert.Equal("visible", XhtmlText.ToPlainText("<a title=\"a > b\" href='x>y'>visible</a>"));

    [Theory]
    [InlineData("1 < 2 and 3 > 2", "1 < 2 and 3 > 2")]
    [InlineData("a<!-- hidden -->b", "ab")]
    [InlineData("a<![CDATA[<kept>]]>b", "a<kept>b")]
    [InlineData("<?xml version='1.0'?><!DOCTYPE html>x", "x")]
    public void Comments_cdata_and_declarations_and_stray_angle_brackets(string html, string expected) =>
        Assert.Equal(expected, XhtmlText.ToPlainText(html));

    [Theory]
    [InlineData("<p>cut off")]
    [InlineData("<p")]
    [InlineData("text <")]
    [InlineData("<!-- never closed")]
    [InlineData("<script>never closed")]
    [InlineData("<![CDATA[never closed")]
    public void Truncated_input_does_not_throw(string html) => Assert.NotNull(XhtmlText.ToPlainText(html));

    [Fact]
    public void A_self_closing_script_tag_does_not_swallow_the_rest() =>
        Assert.Equal("after", XhtmlText.ToPlainText("<script src='x.js'/>after"));
}
