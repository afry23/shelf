using System.Net;
using Shelf.Core.Configuration;
using Shelf.Index;
using Shelf.Semantic;

namespace Shelf.Web;

/// <summary>What the web server needs to start.</summary>
/// <param name="Root">The library root.</param>
/// <param name="Settings">The settings of the run.</param>
/// <param name="Version">The version that <c>/api/v1/status</c> reports.</param>
/// <param name="Address">The address to listen on; see <see cref="BindRules"/>.</param>
/// <param name="Port">The port; 0 lets the system choose one.</param>
/// <param name="Token">The access token, or null for none.</param>
/// <param name="Models">The embedding models that are known, for semantic search.</param>
/// <param name="EmbedderFactory">Opens the embedding model of a folder, on the first semantic request.</param>
/// <param name="MetadataSources">The online catalogs that <c>enrich</c> asks; none if null.</param>
public sealed record WebOptions(
    string Root,
    ShelfSettings Settings,
    string Version,
    IPAddress Address,
    int Port,
    string? Token,
    ModelCatalog Models,
    Func<ModelFolder, IEmbedder> EmbedderFactory,
    IReadOnlyList<IMetadataSource>? MetadataSources = null);

/// <summary>The server could not start or stop as asked (for example the port is in use). The message is meant to be shown.</summary>
public sealed class WebHostException(string message) : Exception(message);
