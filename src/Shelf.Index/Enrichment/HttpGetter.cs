using System.Net;

namespace Shelf.Index;

/// <summary>One answer to a GET request.</summary>
public sealed record HttpAnswer(HttpStatusCode Status, string? ContentType, byte[] Body);

/// <summary>The one place where the online sources touch the network; tests replace it.</summary>
public interface IHttpGetter
{
    /// <exception cref="MetadataSourceException">The server cannot be reached (an answer with an error status is returned, not thrown).</exception>
    HttpAnswer Get(string url, CancellationToken cancellation);
}

/// <summary>Plain HTTPS with an honest user agent and a short timeout.</summary>
public sealed class HttpGetter(string userAgent) : IHttpGetter
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(20) };

    public HttpAnswer Get(string url, CancellationToken cancellation)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.UserAgent.ParseAdd(userAgent);
        try
        {
            using var response = Client.Send(request, cancellation);
            using var stream = response.Content.ReadAsStream(cancellation);
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return new HttpAnswer(response.StatusCode, response.Content.Headers.ContentType?.MediaType, buffer.ToArray());
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !cancellation.IsCancellationRequested)
        {
            throw new MetadataSourceException(e is TaskCanceledException ? "timed out" : e.Message, e);
        }
    }
}
