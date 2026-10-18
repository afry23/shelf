using Shelf.Core.Model;
using Xunit;

namespace Shelf.Core.Tests;

public class TagNamesTests
{
    [Theory]
    [InlineData("devops", "devops")]
    [InlineData("architektur/ddd", "architektur/ddd")]
    [InlineData(" architektur / ddd ", "architektur/ddd")]
    [InlineData("Cloud/Azure", "Cloud/Azure")]
    [InlineData("a/b/c", "a/b/c")]
    public void Valid_tags_are_normalized(string text, string expected)
    {
        Assert.True(TagNames.TryNormalize(text, out var tag, out _));
        Assert.Equal(expected, tag);
    }

    [Theory]
    [InlineData(null, "must not be empty")]
    [InlineData("", "must not be empty")]
    [InlineData("   ", "must not be empty")]
    [InlineData("a//b", "has an empty part")]
    [InlineData("/a", "has an empty part")]
    [InlineData("a/", "has an empty part")]
    [InlineData("a/ /b", "has an empty part")]
    [InlineData("a\tb", "must not contain control characters")]
    public void Invalid_tags_say_why(string? text, string error)
    {
        Assert.False(TagNames.TryNormalize(text, out _, out var actual));
        Assert.Equal(error, actual);
    }

    [Theory]
    [InlineData("cloud", "cloud", true)]
    [InlineData("cloud/azure", "cloud", true)]
    [InlineData("cloud/azure/vm", "cloud/azure", true)]
    [InlineData("cloudy", "cloud", false)]
    [InlineData("cloud", "cloud/azure", false)]
    [InlineData("devops/cloud", "cloud", false)]
    public void IsSameOrBelow_follows_the_hierarchy(string tag, string ancestor, bool expected) =>
        Assert.Equal(expected, TagNames.IsSameOrBelow(tag, ancestor));

    [Theory]
    [InlineData("cloud", "cloud", "infra", "infra")]
    [InlineData("cloud/azure", "cloud", "infra", "infra/azure")]
    [InlineData("cloud/azure/vm", "cloud/azure", "platform", "platform/vm")]
    public void Move_replaces_the_start(string tag, string from, string to, string expected) =>
        Assert.Equal(expected, TagNames.Move(tag, from, to));
}
