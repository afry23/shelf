namespace Shelf.Web;

/// <summary>An answer with the problem document of <see cref="Problems"/>.</summary>
internal sealed class ProblemResult(int status, string detail) : IResult
{
    public Task ExecuteAsync(HttpContext httpContext) => Problems.WriteAsync(httpContext, status, detail);
}
