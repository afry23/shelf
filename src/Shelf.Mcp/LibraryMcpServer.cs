using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Shelf.Core.Configuration;

namespace Shelf.Mcp;

/// <summary>
/// The MCP server of a library (PRD 7.2, ADR 0006): tools and resources registered by hand with a source-generated JSON
/// context, no reflection. Standard output carries the protocol and nothing else; every log line goes to the logger
/// factory, which writes to standard error.
/// </summary>
public static class LibraryMcpServer
{
    private const string Instructions =
        "Tools for an e-book library. Use search_books to find works and places in their text, get_book for the details and "
        + "table of contents of a work, and read_passage to read the text at a location from either. "
        + "Locations such as 'page=12' are given in the results; pass them on unchanged.";

    /// <summary>Runs until the input ends or <paramref name="cancellation"/> is cancelled.</summary>
    public static async Task RunAsync(
        string root,
        ShelfSettings settings,
        string version,
        Stream input,
        Stream output,
        ILoggerFactory loggerFactory,
        CancellationToken cancellation)
    {
        ArgumentException.ThrowIfNullOrEmpty(root);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        var tools = new LibraryTools(root, settings);
        var options = new McpServerOptions
        {
            ServerInfo = new Implementation { Name = "shelf", Version = version },
            ServerInstructions = Instructions,
            ToolCollection = Tools(tools),
            ResourceCollection = Resources(tools),
        };

        var transport = new StreamServerTransport(input, output, "shelf", loggerFactory);
        await using var server = McpServer.Create(transport, options, loggerFactory);
        await server.RunAsync(cancellation).ConfigureAwait(false);
    }

    internal static McpServerPrimitiveCollection<McpServerTool> Tools(LibraryTools tools)
    {
        var json = McpJsonContext.SharedOptions;
        var collection = new McpServerPrimitiveCollection<McpServerTool>();
        collection.Add(McpServerTool.Create((Func<string, int, SearchBooksResult>)tools.SearchBooks, Read("search_books", json)));
        collection.Add(McpServerTool.Create((Func<string, BookDetails>)tools.GetBook, Read("get_book", json)));
        collection.Add(McpServerTool.Create((Func<string, string, int?, int, string?, PassageText>)tools.ReadPassage, Read("read_passage", json)));
        collection.Add(McpServerTool.Create((Func<TagTree>)tools.ListTags, Read("list_tags", json)));
        collection.Add(McpServerTool.Create((Func<DuplicatesResult>)tools.FindDuplicates, Read("find_duplicates", json)));
        collection.Add(McpServerTool.Create((Func<string?, OrganizationProposal>)tools.ProposeOrganization, Read("propose_organization", json)));
        return collection;
    }

    internal static McpServerResourceCollection Resources(LibraryTools tools)
    {
        var json = McpJsonContext.SharedOptions;
        var collection = new McpServerResourceCollection();
        collection.Add(McpServerResource.Create((Func<string, string>)tools.BookJson, new McpServerResourceCreateOptions
        {
            UriTemplate = "shelf://book/{id}",
            Name = "book",
            Description = "Metadata of a work (the table of contents is at shelf://book/{id}/toc).",
            MimeType = "application/json",
            SerializerOptions = json,
        }));
        collection.Add(McpServerResource.Create((Func<string, string>)tools.TocJson, new McpServerResourceCreateOptions
        {
            UriTemplate = "shelf://book/{id}/toc",
            Name = "book-toc",
            Description = "Table of contents of a work: title, level and a location for read_passage.",
            MimeType = "application/json",
            SerializerOptions = json,
        }));
        return collection;
    }

    private static McpServerToolCreateOptions Read(string name, System.Text.Json.JsonSerializerOptions json) => new()
    {
        Name = name,
        UseStructuredContent = true,
        ReadOnly = true,
        Destructive = false,
        SerializerOptions = json,
    };
}
