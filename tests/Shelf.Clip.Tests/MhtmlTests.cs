using System.Text;
using Xunit;

namespace Shelf.Clip.Tests;

public sealed class MhtmlTests
{
    private static byte[] Saved(string htmlPart, string lineBreak = "\n") => Encoding.Latin1.GetBytes(string.Join(
        lineBreak,
        "From: <Saved by Blink>",
        "Snapshot-Content-Location: https://blog.example.com/post",
        "MIME-Version: 1.0",
        "Content-Type: multipart/related; type=\"text/html\"; boundary=\"B\"",
        string.Empty,
        "--B",
        htmlPart,
        "--B",
        "Content-Type: image/png",
        "Content-Transfer-Encoding: base64",
        "Content-Location: https://cdn.example.com/a.png",
        string.Empty,
        Convert.ToBase64String(Pictures.Png(640, 480)),
        "--B--",
        string.Empty));

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void The_html_and_the_files_are_found_by_their_address(string lineBreak)
    {
        var html = string.Join(lineBreak, "Content-Type: text/html; charset=\"utf-8\"", "Content-Transfer-Encoding: 8bit", "Content-Location: https://blog.example.com/post", string.Empty, "<p>Hello</p>");

        var page = Mhtml.Parse(Saved(html, lineBreak));

        Assert.Equal("<p>Hello</p>", page.Html.Trim());
        Assert.Equal("https://blog.example.com/post", page.Location);
        Assert.Equal(("png", 640, 480), ImageProbe.Read(page.Find("https://cdn.example.com/a.png")!));
        Assert.Null(page.Find("https://cdn.example.com/other.png"));
    }

    [Fact]
    public void Quoted_printable_html_is_decoded()
    {
        var html = "Content-Type: text/html; charset=\"utf-8\"\nContent-Transfer-Encoding: quoted-printable\n\n<p>Caf=C3=A9 and a very long line that is =\ncut</p>";

        var page = Mhtml.Parse(Saved(html));

        Assert.Equal("<p>Café and a very long line that is cut</p>", page.Html.Trim());
    }

    [Fact]
    public void A_file_without_html_is_refused() =>
        Assert.Throws<FormatException>(() => Mhtml.Parse(Saved("Content-Type: text/plain\n\nnothing")));

    [Fact]
    public void A_file_without_a_boundary_is_refused() =>
        Assert.Throws<FormatException>(() => Mhtml.Parse(Encoding.ASCII.GetBytes("Content-Type: text/html\n\n<p>x</p>")));

    [Theory]
    [InlineData("From: <Saved by Blink>\nSubject: x", true)]
    [InlineData("MIME-Version: 1.0", true)]
    [InlineData("<!DOCTYPE html><html>", false)]
    public void An_mhtml_file_is_told_from_html(string start, bool expected) => Assert.Equal(expected, Mhtml.Looks(start));
}
