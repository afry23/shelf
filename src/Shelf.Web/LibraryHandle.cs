using Shelf.Library;
using Shelf.Semantic;

namespace Shelf.Web;

/// <summary>
/// The <see cref="LibraryService"/> of the server, made on the first request that needs it: <c>serve</c> starts without
/// opening the index or the embedding model, and a server nobody asks stays small. One service for the whole run, so
/// that its write gate covers every request.
/// </summary>
internal sealed class LibraryHandle(WebOptions options) : IDisposable
{
    private readonly Lock _gate = new();
    private SemanticBackend? _semantic;
    private LibraryService? _service;

    public LibraryService Service
    {
        get
        {
            lock (_gate)
            {
                _semantic ??= new SemanticBackend(options.Models, options.EmbedderFactory);
                return _service ??= new LibraryService(options.Root, options.Settings, _semantic, options.MetadataSources, options.Pages);
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _semantic?.Dispose();
            _semantic = null;
            _service = null;
        }
    }
}
