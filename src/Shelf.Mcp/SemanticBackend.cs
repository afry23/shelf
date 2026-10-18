using Shelf.Semantic;

namespace Shelf.Mcp;

/// <summary>
/// The embedding model for the queries of <c>semantic_search</c>. A server lives for a whole session, so the model is
/// opened on the first query and kept (opening it costs about half a second; a query costs milliseconds). Nothing here
/// downloads: a missing model is reported, never fetched (PRD 9.4).
/// </summary>
internal sealed class SemanticBackend(ModelCatalog catalog, Func<ModelFolder, IEmbedder> factory) : IDisposable
{
    private readonly Lock _gate = new();
    private IEmbedder? _embedder;
    private string? _folder;

    public ModelCatalog Catalog => catalog;

    public float[] EmbedQuery(ModelFolder folder, string text)
    {
        lock (_gate)
        {
            if (_embedder is null || _folder != folder.Directory)
            {
                (_embedder as IDisposable)?.Dispose();
                _embedder = factory(folder);
                _folder = folder.Directory;
            }

            return _embedder.EmbedQuery(text);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            (_embedder as IDisposable)?.Dispose();
            _embedder = null;
        }
    }
}
