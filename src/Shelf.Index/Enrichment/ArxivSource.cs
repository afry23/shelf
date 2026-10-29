using System.Net;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Shelf.Core.Model;

namespace Shelf.Index;

/// <summary>The arXiv API (https://export.arxiv.org/api): preprints, found by arXiv ID or by title and author (F-PAP-3).</summary>
public sealed partial class ArxivSource(IHttpGetter http) : IMetadataSource
{
    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";
    private static readonly XNamespace Arxiv = "http://arxiv.org/schemas/atom";

    public string Name => "arxiv";

    public bool ForPapers => true;

    public IReadOnlyList<OnlineBook> FindByIsbn(string isbn13, CancellationToken cancellation) => [];

    public IReadOnlyList<OnlineBook> FindByArxivId(string arxivId, CancellationToken cancellation) =>
        Query($"https://export.arxiv.org/api/query?id_list={Uri.EscapeDataString(arxivId)}", cancellation);

    public IReadOnlyList<OnlineBook> Search(string title, string? author, CancellationToken cancellation)
    {
        var search = $"ti:\"{title.Replace("\"", string.Empty, StringComparison.Ordinal)}\"";
        if (author is not null)
        {
            search += $" AND au:\"{author.Replace("\"", string.Empty, StringComparison.Ordinal)}\"";
        }

        return Query($"https://export.arxiv.org/api/query?search_query={Uri.EscapeDataString(search)}&max_results=5", cancellation);
    }

    public byte[]? FetchPdf(OnlineBook paper, CancellationToken cancellation)
    {
        if (paper.ArxivId is not { } id)
        {
            return null;
        }

        var answer = http.Get($"https://arxiv.org/pdf/{id}", cancellation);
        if (answer.Status == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (answer.Status != HttpStatusCode.OK)
        {
            throw new MetadataSourceException($"HTTP {(int)answer.Status} {answer.Status}");
        }

        // A withdrawn paper is answered with an HTML page, not with a PDF.
        return answer.Body.AsSpan().StartsWith("%PDF"u8) ? answer.Body : null;
    }

    public OnlineCover? FetchCover(OnlineBook book, CancellationToken cancellation) => null;

    private List<OnlineBook> Query(string url, CancellationToken cancellation)
    {
        var answer = http.Get(url, cancellation);
        if (answer.Status != HttpStatusCode.OK)
        {
            throw new MetadataSourceException($"HTTP {(int)answer.Status} {answer.Status}");
        }

        XDocument feed;
        try
        {
            using var stream = new MemoryStream(answer.Body);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            feed = XDocument.Load(reader);
        }
        catch (XmlException e)
        {
            throw new MetadataSourceException("the answer is not XML", e);
        }

        var books = new List<OnlineBook>();
        foreach (var entry in feed.Descendants(Atom + "entry"))
        {
            // The ID of an entry is a link, http://arxiv.org/abs/1706.03762v7.
            var id = PaperIds.Parse(Value(entry, Atom + "id"))?.ArxivId;
            var title = Collapse(Value(entry, Atom + "title"));

            // A bad request is answered with an entry titled "Error" that has no ID.
            if (id is null || title is null)
            {
                continue;
            }

            var published = Value(entry, Atom + "published");
            books.Add(new OnlineBook(
                Name,
                null,
                title,
                [.. entry.Elements(Atom + "author").Select(a => Collapse(Value(a, Atom + "name"))).OfType<string>()],
                Year: published is { Length: >= 4 } && int.TryParse(published.AsSpan(0, 4), out var year) ? year : null,
                Doi: Value(entry, Arxiv + "doi")?.Trim().ToLowerInvariant(),
                ArxivId: id,
                Venue: Collapse(Value(entry, Arxiv + "journal_ref")),
                Abstract: Collapse(Value(entry, Atom + "summary"))));
        }

        return books;
    }

    private static string? Value(XElement parent, XName name) => parent.Element(name)?.Value;

    // Titles and abstracts are wrapped at 80 columns.
    private static string? Collapse(string? text) => string.IsNullOrWhiteSpace(text) ? null : Spaces().Replace(text, " ").Trim();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
