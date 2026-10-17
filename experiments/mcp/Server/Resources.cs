using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Proto;

internal static class Resources
{
    public static string BookJson(string id)
    {
        var book = Data.Find(id) ?? throw new McpProtocolException($"Unknown book '{id}'.", McpErrorCode.InvalidParams);
        return JsonSerializer.Serialize(book, ProtoJsonContext.Default.Book);
    }

    public static string TocJson(string id)
    {
        _ = Data.Find(id) ?? throw new McpProtocolException($"Unknown book '{id}'.", McpErrorCode.InvalidParams);
        return JsonSerializer.Serialize(Data.B1Toc, ProtoJsonContext.Default.TocEntryArray);
    }

    public static IEnumerable<McpServerResource> Create()
    {
        var options = ProtoJsonContext.SharedOptions;
        yield return McpServerResource.Create((Func<string, string>)BookJson, new()
        {
            UriTemplate = "shelf://book/{id}",
            Name = "book",
            Description = "Metadata of a work.",
            MimeType = "application/json",
            SerializerOptions = options,
        });
        yield return McpServerResource.Create((Func<string, string>)TocJson, new()
        {
            UriTemplate = "shelf://book/{id}/toc",
            Name = "book-toc",
            Description = "Table of contents of a work.",
            MimeType = "application/json",
            SerializerOptions = options,
        });
    }
}
