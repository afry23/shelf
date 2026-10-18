namespace Shelf.Web;

/// <summary>
/// Runs a file result and, when the answer is <c>416</c> (the range lies beyond the end), gives it the problem document of
/// <see cref="Problems"/>. The status code pages only complete an answer without a type, and the file result has already set
/// the type of the file, which would label the empty body of the <c>416</c> as a book.
/// </summary>
internal sealed class RangeProblemResult(IResult file) : IResult
{
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        await file.ExecuteAsync(httpContext).ConfigureAwait(false);

        var response = httpContext.Response;
        if (response.StatusCode == StatusCodes.Status416RangeNotSatisfiable && !response.HasStarted)
        {
            response.ContentType = null;
            response.ContentLength = null;
            await Problems.WriteAsync(httpContext, StatusCodes.Status416RangeNotSatisfiable, "the range lies beyond the end of the file").ConfigureAwait(false);
        }
    }
}
