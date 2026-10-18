using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace Shelf.Web;

/// <summary>The one error format of <c>/api/v1</c>: <c>application/problem+json</c> with <c>status</c>, <c>title</c> and <c>detail</c>.</summary>
internal static class Problems
{
    public const string ContentType = "application/problem+json";

    public static Task WriteAsync(HttpContext context, int status, string? detail = null)
    {
        var response = context.Response;
        response.StatusCode = status;
        response.ContentType = ContentType;
        var title = ReasonPhrases.GetReasonPhrase(status);

        // Bytes, not WriteAsync(string): that would add "; charset=utf-8" to a type that is UTF-8 by definition.
        var body = JsonSerializer.SerializeToUtf8Bytes(new ApiProblem(status, title, detail), WebJsonContext.Default.ApiProblem);
        response.ContentLength = body.Length;
        return response.Body.WriteAsync(body).AsTask();
    }
}
