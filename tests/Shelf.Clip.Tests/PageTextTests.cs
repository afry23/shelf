using System.Text;
using Xunit;

namespace Shelf.Clip.Tests;

public sealed class PageTextTests
{
    static PageTextTests() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    [Fact]
    public void Utf8_is_the_default() => Assert.Equal("<p>Café</p>", PageText.Decode(Encoding.UTF8.GetBytes("<p>Café</p>")));

    [Fact]
    public void A_byte_order_mark_is_dropped() =>
        Assert.Equal("<p>x</p>", PageText.Decode([.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes("<p>x</p>")]));

    [Fact]
    public void The_charset_of_the_page_is_followed() =>
        Assert.Equal("<meta charset=\"iso-8859-1\"><p>Café</p>", PageText.Decode(Encoding.Latin1.GetBytes("<meta charset=\"iso-8859-1\"><p>Café</p>")));

    [Fact]
    public void An_unknown_charset_falls_back_to_utf8() =>
        Assert.Equal("<meta charset=\"nonsense-9\"><p>Café</p>", PageText.Decode(Encoding.UTF8.GetBytes("<meta charset=\"nonsense-9\"><p>Café</p>")));
}
