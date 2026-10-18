using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Shelf.Web.Tests;

public sealed class ProblemsTests
{
    [Theory]
    [InlineData(404, "Not Found")]
    [InlineData(405, "Method Not Allowed")]
    [InlineData(421, "Misdirected Request")]
    public async Task A_problem_has_the_status_the_title_and_the_media_type(int status, string title)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await Problems.WriteAsync(context, status);

        Assert.Equal(status, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);
        var document = JsonNode.Parse(((MemoryStream)context.Response.Body).ToArray())!;
        Assert.Equal(status, document["status"]!.GetValue<int>());
        Assert.Equal(title, document["title"]!.GetValue<string>());
        Assert.Null(document["detail"]);
        Assert.Equal(2, document.AsObject().Count);
    }

    [Fact]
    public async Task A_detail_is_written_when_there_is_one()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await Problems.WriteAsync(context, 401, "a valid access token is required");

        var document = JsonNode.Parse(((MemoryStream)context.Response.Body).ToArray())!;
        Assert.Equal("a valid access token is required", document["detail"]!.GetValue<string>());
        Assert.Equal(context.Response.ContentLength, ((MemoryStream)context.Response.Body).Length);
    }
}
