namespace Proto.Mcp;

/// <summary>Fixed in-memory data for the spike; no files, no database.</summary>
internal static class Data
{
    public static readonly Book B1 = new(
        "B1",
        "Event Streams in Practice",
        ["Jördis Ålander"],
        [new FormatInfo("pdf", 1_048_576, 212), new FormatInfo("epub", 524_288, null)],
        ["tech/streams", "reference"]);

    public static readonly TocEntry[] B1Toc =
    [
        new("Introduction", 1),
        new("Logs and Streams", 17),
        new("Backpressure", 88),
    ];

    public static Book? Find(string id) => id == B1.Id ? B1 : null;

    public static TagNode Tags() => new("root", 9,
    [
        new("tech", 7, [new TagNode("streams", 4, []), new TagNode("databases", 3, [])]),
        new("reference", 2, []),
    ]);
}
