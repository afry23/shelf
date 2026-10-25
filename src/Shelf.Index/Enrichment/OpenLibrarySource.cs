using System.Text.Json;

namespace Shelf.Index;

/// <summary>Open Library (https://openlibrary.org): no key, open data.</summary>
public sealed class OpenLibrarySource(IHttpGetter http) : IMetadataSource
{
    public string Name => "openlibrary";

    public IReadOnlyList<OnlineBook> FindByIsbn(string isbn13, CancellationToken cancellation)
    {
        var key = $"ISBN:{isbn13}";
        using var document = OnlineParsing.Parse(
            http.Get($"https://openlibrary.org/api/books?bibkeys={key}&jscmd=details&format=json", cancellation),
            "ISBN lookup");
        if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty(key, out var entry)
            || !entry.TryGetProperty("details", out var details))
        {
            return [];
        }

        var title = OnlineParsing.Text(details, "title");
        if (title is null)
        {
            return [];
        }

        var authors = OnlineParsing.Items(details, "authors").Select(a => OnlineParsing.Text(a, "name")).OfType<string>().ToList();
        var language = OnlineParsing.Items(details, "languages").Select(l => OnlineParsing.Text(l, "key")).FirstOrDefault();
        return
        [
            new OnlineBook(
                Name,
                isbn13,
                title,
                authors,
                OnlineParsing.Text(details, "subtitle"),
                OnlineParsing.Items(details, "publishers").Select(p => p.ValueKind == JsonValueKind.String ? OnlineParsing.Clean(p.GetString()) : null).FirstOrDefault(p => p is not null),
                OnlineParsing.Year(OnlineParsing.Text(details, "publish_date")),
                OnlineParsing.Language(language),
                OnlineParsing.Text(details, "description"),
                $"https://covers.openlibrary.org/b/isbn/{isbn13}-L.jpg?default=false"),
        ];
    }

    public IReadOnlyList<OnlineBook> Search(string title, string? author, CancellationToken cancellation)
    {
        var query = $"https://openlibrary.org/search.json?title={Uri.EscapeDataString(title)}&limit=10"
            + "&fields=title,subtitle,author_name,publisher,first_publish_year,language,isbn,cover_i";
        if (author is not null)
        {
            query += $"&author={Uri.EscapeDataString(author)}";
        }

        using var document = OnlineParsing.Parse(http.Get(query, cancellation), "search");
        var books = new List<OnlineBook>();
        foreach (var doc in OnlineParsing.Items(document.RootElement, "docs"))
        {
            var found = OnlineParsing.Text(doc, "title");
            if (found is null)
            {
                continue;
            }

            var cover = doc.TryGetProperty("cover_i", out var id) && id.ValueKind == JsonValueKind.Number
                ? $"https://covers.openlibrary.org/b/id/{id.GetInt64()}-L.jpg?default=false"
                : null;
            books.Add(new OnlineBook(
                Name,
                OnlineParsing.FirstIsbn13(OnlineParsing.Items(doc, "isbn").Select(i => i.GetString())),
                found,
                [.. OnlineParsing.Items(doc, "author_name").Select(a => OnlineParsing.Clean(a.GetString())).OfType<string>()],
                OnlineParsing.Text(doc, "subtitle"),
                OnlineParsing.Items(doc, "publisher").Select(p => OnlineParsing.Clean(p.GetString())).FirstOrDefault(p => p is not null),
                doc.TryGetProperty("first_publish_year", out var year) && year.ValueKind == JsonValueKind.Number ? year.GetInt32() : null,
                OnlineParsing.Items(doc, "language").Select(l => OnlineParsing.Language(l.GetString())).FirstOrDefault(l => l is not null),
                null,
                cover));
        }

        return books;
    }

    public OnlineCover? FetchCover(OnlineBook book, CancellationToken cancellation) => OnlineParsing.Cover(http, book.CoverUrl, cancellation);
}
