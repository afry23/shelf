namespace Shelf.Semantic;

/// <summary>Opens the bytes behind a URL. The only place where shelf touches the network, and only for <c>model install</c>.</summary>
public interface IModelDownloader
{
    /// <exception cref="IOException">The file cannot be fetched.</exception>
    Stream Open(string url);
}

/// <summary>Downloads over HTTPS.</summary>
public sealed class HttpModelDownloader : IModelDownloader
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromHours(1) };

    public Stream Open(string url)
    {
        try
        {
            var response = Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();
            return response.Content.ReadAsStream();
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            throw new IOException(e.Message, e);
        }
    }
}
