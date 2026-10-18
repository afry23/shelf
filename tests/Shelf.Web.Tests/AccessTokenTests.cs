using Xunit;

namespace Shelf.Web.Tests;

public sealed class AccessTokenTests
{
    [Theory]
    [InlineData("s3cret", "s3cret", true)]
    [InlineData("s3cret", "S3CRET", false)]
    [InlineData("s3cret", "s3cret ", false)]
    [InlineData("s3cret", "s3cre", false)]
    [InlineData("s3cret", "", false)]
    [InlineData("s3cret", null, false)]
    [InlineData("Größe", "Größe", true)]
    public void A_token_matches_only_when_it_is_equal(string expected, string? given, bool matches) =>
        Assert.Equal(matches, AccessToken.Matches(expected, given));

    [Theory]
    [InlineData("Bearer abc", "abc")]
    [InlineData("bearer abc", "abc")]
    [InlineData("BEARER abc", "abc")]
    [InlineData("Bearer a b", "a b")]
    [InlineData("Basic abc", null)]
    [InlineData("Bearer", null)]
    [InlineData("Bearer ", null)]
    [InlineData("Bearerabc", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void The_token_is_read_after_the_bearer_scheme(string? header, string? expected) =>
        Assert.Equal(expected, AccessToken.FromAuthorization(header));
}
