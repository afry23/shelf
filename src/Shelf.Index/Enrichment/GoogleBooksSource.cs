using System.Text.Json;

namespace Shelf.Index;

/// <summary>Google Books (https://www.googleapis.com/books/v1), without an API key.</summary>
public sealed class GoogleBooksSource(IHttpGetter http) : IMetadataSource
{
    public string Name => "google";

    public IReadOnlyList<OnlineBook> FindByIsbn(string isbn13, CancellationToken cancellation) =>
        Query($"isbn:{isbn13}", cancellation);

    public IReadOnlyList<OnlineBook> Search(string title, string? author, CancellationToken cancellation) =>
        Query(author is null ? $"intitle:{title}" : $"intitle:{title} inauthor:{author}", cancellation);

    public OnlineCover? FetchCover(OnlineBook book, CancellationToken cancellation) => OnlineParsing.Cover(http, book.CoverUrl, cancellation);

    private List<OnlineBook> Query(string q, CancellationToken cancellation)
    {
        using var document = OnlineParsing.Parse(
            http.Get($"https://www.googleapis.com/books/v1/volumes?q={Uri.EscapeDataString(q)}&maxResults=10", cancellation),
            "search");
        var books = new List<OnlineBook>();
        foreach (var item in OnlineParsing.Items(document.RootElement, "items"))
        {
            if (!item.TryGetProperty("volumeInfo", out var info) || OnlineParsing.Text(info, "title") is not { } title)
            {
                continue;
            }

            var identifiers = OnlineParsing.Items(info, "industryIdentifiers").Select(i => OnlineParsing.Text(i, "identifier"));
            var cover = info.TryGetProperty("imageLinks", out var links) ? OnlineParsing.Text(links, "thumbnail") : null;
            books.Add(new OnlineBook(
                Name,
                OnlineParsing.FirstIsbn13(identifiers),
                title,
                [.. OnlineParsing.Items(info, "authors").Select(a => OnlineParsing.Clean(a.GetString())).OfType<string>()],
                OnlineParsing.Text(info, "subtitle"),
                OnlineParsing.Text(info, "publisher"),
                OnlineParsing.Year(OnlineParsing.Text(info, "publishedDate")),
                OnlineParsing.Language(OnlineParsing.Text(info, "language")),
                OnlineParsing.Text(info, "description"),
                cover?.Replace("http://", "https://", StringComparison.Ordinal)));
        }

        return books;
    }
}
