using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Proto;

internal static class Tools
{
    private const int MaxCharsDefault = 4_000;
    private const int MaxCharsHard = 20_000;

    public static Book GetBook(string id) =>
        Data.Find(id) ?? throw new McpException($"Unknown book '{id}'.");

    public static string ReadPassage(string id, string location, int maxChars = MaxCharsDefault)
    {
        // A tool-side log line; must end up on stderr, never on stdout (check row M9).
        Console.Error.WriteLine($"read_passage id={id} location={location} maxChars={maxChars}");
        if (maxChars > MaxCharsHard)
            throw new McpException($"maxChars {maxChars} exceeds the hard maximum {MaxCharsHard}.");
        _ = Data.Find(id) ?? throw new McpException($"Unknown book '{id}'.");
        return $"[{id}@{location}]" + new string('a', maxChars);
    }

    public static TagNode ListTags() => Data.Tags();

    public static AddTagsResult AddTags(string id, string[] tags)
    {
        var book = Data.Find(id) ?? throw new McpException($"Unknown book '{id}'.");
        var merged = book.Tags.Union(tags).ToArray();
        return new AddTagsResult(id, merged, merged.Length - book.Tags.Length);
    }

    public static IEnumerable<McpServerTool> Create()
    {
        var options = ProtoJsonContext.SharedOptions;
        yield return McpServerTool.Create((Func<string, Book>)GetBook, new()
        {
            Name = "get_book",
            Description = "Metadata, formats and tags of a work.",
            UseStructuredContent = true,
            ReadOnly = true,
            SerializerOptions = options,
        });
        yield return McpServerTool.Create((Func<string, string, int, string>)ReadPassage, new()
        {
            Name = "read_passage",
            Description = "Text excerpt at a location. maxChars: default 4,000, hard maximum 20,000.",
            ReadOnly = true,
            SerializerOptions = options,
        });
        yield return McpServerTool.Create((Func<TagNode>)ListTags, new()
        {
            Name = "list_tags",
            Description = "Tag tree with counts.",
            UseStructuredContent = true,
            ReadOnly = true,
            SerializerOptions = options,
        });
        yield return McpServerTool.Create((Func<string, string[], AddTagsResult>)AddTags, new()
        {
            Name = "add_tags",
            Description = "Add tags to a work (write).",
            UseStructuredContent = true,
            SerializerOptions = options,
        });
    }
}
