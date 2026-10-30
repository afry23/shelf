using Xunit;

namespace Shelf.Clip.Tests;

public sealed class ImageProbeTests
{
    [Fact]
    public void A_png_has_its_size_read() => Assert.Equal(("png", 1200, 800), ImageProbe.Read(Pictures.Png(1200, 800)));

    [Fact]
    public void A_gif_has_its_size_read() => Assert.Equal(("gif", 1, 1), ImageProbe.Read(Pictures.Gif(1, 1)));

    [Fact]
    public void A_jpeg_has_its_size_read() => Assert.Equal(("jpg", 1600, 900), ImageProbe.Read(Pictures.Jpeg(1600, 900)));

    [Theory]
    [InlineData("<html></html>")]
    [InlineData("")]
    public void Bytes_that_are_no_picture_give_nothing(string text) => Assert.Null(ImageProbe.Read(System.Text.Encoding.UTF8.GetBytes(text)));
}
