using Xunit;

namespace Shelf.Semantic.Tests;

public sealed class WorkTextTests
{
    [Fact]
    public void The_title_text_has_title_and_subtitle_but_no_description_or_tags()
    {
        Assert.Equal("Kubernetes Patterns\nReusable Elements", WorkText.BuildTitle("Kubernetes Patterns", " Reusable Elements "));
        Assert.Equal("Cloud Native", WorkText.BuildTitle("Cloud Native", null));
    }
}
