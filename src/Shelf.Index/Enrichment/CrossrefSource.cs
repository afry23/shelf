using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Shelf.Core.Model;

namespace Shelf.Index;

/// <summary>Crossref (https://api.crossref.org): the records of publishers, found by DOI or by title and author (F-PAP-3).</summary>
public sealed partial class CrossrefSource(IHttpGetter http) : IMetadataSource
{
    public string Name => "crossref";

    public bool ForPapers => true;

    public IReadOnlyList<OnlineBook> FindByIsbn(string isbn13, CancellationToken cancellation) => [];

    public IReadOnlyList<OnlineBook> FindByDoi(string doi, CancellationToken cancellation)
    {
        var path = string.Join('/', doi.Split('/').Select(Uri.EscapeDataString));
        var answer = http.Get($"https://api.crossref.org/works/{path}", cancellation);
        if (answer.Status == HttpStatusCode.NotFound)
        {
            return [];
        }

        using var document = OnlineParsing.Parse(answer, "DOI lookup");
        return document.RootElement.TryGetProperty("message", out var message) && Read(message) is { } book ? [book] : [];
    }

    public IReadOnlyList<Reference>? FindReferences(string doi, CancellationToken cancellation)
    {
        var path = string.Join('/', doi.Split('/').Select(Uri.EscapeDataString));
        var answer = http.Get($"https://api.crossref.org/works/{path}", cancellation);
        if (answer.Status == HttpStatusCode.NotFound)
        {
            return null;
        }

        using var document = OnlineParsing.Parse(answer, "reference lookup");
        if (!document.RootElement.TryGetProperty("message", out var message))
        {
            return null;
        }

        var references = new List<Reference>();
        foreach (var item in OnlineParsing.Items(message, "reference"))
        {
            var referenceDoi = OnlineParsing.Text(item, "DOI")?.Trim().ToLowerInvariant();
            var title = OnlineParsing.Clean(OnlineParsing.Text(item, "article-title") ?? OnlineParsing.Text(item, "volume-title"));
            var unstructured = OnlineParsing.Clean(OnlineParsing.Text(item, "unstructured"));
            if (string.IsNullOrEmpty(referenceDoi) && title is null && unstructured is null)
            {
                continue;
            }

            references.Add(new Reference(
                string.IsNullOrEmpty(referenceDoi) ? null : referenceDoi,
                title,
                int.TryParse(OnlineParsing.Text(item, "year"), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var year) ? year : null,
                OnlineParsing.Clean(OnlineParsing.Text(item, "author")),
                unstructured));
        }

        return references;
    }

    public IReadOnlyList<OnlineBook> Search(string title, string? author, CancellationToken cancellation)
    {
        var query = $"https://api.crossref.org/works?query.bibliographic={Uri.EscapeDataString(title)}&rows=5";
        if (author is not null)
        {
            query += $"&query.author={Uri.EscapeDataString(author)}";
        }

        using var document = OnlineParsing.Parse(http.Get(query, cancellation), "search");
        if (!document.RootElement.TryGetProperty("message", out var message))
        {
            return [];
        }

        return [.. OnlineParsing.Items(message, "items").Select(Read).OfType<OnlineBook>()];
    }

    public OnlineCover? FetchCover(OnlineBook book, CancellationToken cancellation) => null;

    private OnlineBook? Read(JsonElement item)
    {
        var title = FirstText(item, "title");
        if (title is null)
        {
            return null;
        }

        var authors = new List<string>();
        foreach (var author in OnlineParsing.Items(item, "author"))
        {
            var family = OnlineParsing.Text(author, "family");
            var given = OnlineParsing.Text(author, "given");
            var name = family is null ? OnlineParsing.Text(author, "name") : given is null ? family : $"{given} {family}";
            if (name is not null)
            {
                authors.Add(name);
            }
        }

        return new OnlineBook(
            Name,
            null,
            title,
            authors,
            FirstText(item, "subtitle"),
            OnlineParsing.Text(item, "publisher"),
            YearOf(item),
            OnlineParsing.Text(item, "language"),
            null,
            null,
            OnlineParsing.Text(item, "DOI")?.ToLowerInvariant(),
            null,
            FirstText(item, "container-title"),
            Abstract(OnlineParsing.Text(item, "abstract")),
            OnlineParsing.Text(item, "volume"),
            OnlineParsing.Text(item, "issue"),
            OnlineParsing.Text(item, "page"));
    }

    private static string? FirstText(JsonElement parent, string name) =>
        OnlineParsing.Items(parent, name).Select(e => e.ValueKind == JsonValueKind.String ? OnlineParsing.Clean(e.GetString()) : null).FirstOrDefault(t => t is not null);

    private static int? YearOf(JsonElement item)
    {
        foreach (var field in new[] { "issued", "published-print", "published-online", "published" })
        {
            if (item.TryGetProperty(field, out var date)
                && OnlineParsing.Items(date, "date-parts").FirstOrDefault() is { ValueKind: JsonValueKind.Array } parts
                && parts.GetArrayLength() > 0
                && parts[0].ValueKind == JsonValueKind.Number)
            {
                return parts[0].GetInt32();
            }
        }

        return null;
    }

    // Crossref abstracts are JATS XML: <jats:p>Text</jats:p>. Only the text is wanted, and the heading "Abstract" is not part of it.
    private static string? Abstract(string? jats)
    {
        if (jats is null)
        {
            return null;
        }

        var text = Tags().Replace(jats, " ");
        text = WebUtility.HtmlDecode(Spaces().Replace(text, " ")).Trim();
        if (text.StartsWith("Abstract ", StringComparison.OrdinalIgnoreCase))
        {
            text = text["Abstract ".Length..].TrimStart();
        }

        return OnlineParsing.Clean(text);
    }

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
