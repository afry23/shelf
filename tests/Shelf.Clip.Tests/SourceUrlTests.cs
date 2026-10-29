using Xunit;

namespace Shelf.Clip.Tests;

public sealed class SourceUrlTests
{
    [Theory]
    [InlineData("https://blog.example.com/why-tests-matter", "https://blog.example.com/why-tests-matter")]
    [InlineData("https://blog.example.com/why-tests-matter#comments", "https://blog.example.com/why-tests-matter")]
    [InlineData("https://blog.example.com/why-tests-matter?utm_source=feed", "https://blog.example.com/why-tests-matter")]
    [InlineData("https://Blog.Example.com/why-tests-matter?utm_source=a&utm_medium=email", "https://blog.example.com/why-tests-matter")]
    [InlineData("https://example.com/read?id=7&utm_campaign=x&fbclid=y", "https://example.com/read?id=7")]
    public void Fragment_and_tracking_parameters_are_dropped(string url, string expected) => Assert.Equal(expected, SourceUrl.Normalize(url));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("post.html")]
    [InlineData("file:///tmp/post.html")]
    public void An_address_that_is_no_web_address_gives_nothing(string? url) => Assert.Null(SourceUrl.Normalize(url));
}
