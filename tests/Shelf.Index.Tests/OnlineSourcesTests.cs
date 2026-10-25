using System.Net;
using System.Text;
using Xunit;

namespace Shelf.Index.Tests;

public sealed class OnlineSourcesTests
{
    private static readonly CancellationToken None = CancellationToken.None;

    [Theory]
    [InlineData("2019", 2019)]
    [InlineData("2019-12-03", 2019)]
    [InlineData("March 2020", 2020)]
    [InlineData("Mar 3, 1999", 1999)]
    [InlineData("12345", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Year_is_the_first_standalone_four_digit_number(string? date, int? expected) =>
        Assert.Equal(expected, OnlineParsing.Year(date));

    [Theory]
    [InlineData("eng", "en")]
    [InlineData("/languages/ger", "de")]
    [InlineData("DE", "de")]
    [InlineData("xyz", null)]
    [InlineData(null, null)]
    public void Language_becomes_a_two_letter_code(string? code, string? expected) =>
        Assert.Equal(expected, OnlineParsing.Language(code));

    [Fact]
    public void OpenLibrary_reads_an_edition_by_ISBN()
    {
        var http = new StubHttp("""
            { "ISBN:9781492034926": { "details": {
                "title": "Kubernetes Patterns", "subtitle": "Reusable Elements",
                "authors": [{ "name": "Bilgin Ibryam" }, { "name": "Roland Huß" }],
                "publishers": ["O'Reilly Media"], "publish_date": "April 2019",
                "languages": [{ "key": "/languages/eng" }],
                "description": { "type": "/type/text", "value": "Patterns." } } } }
            """);
        var book = Assert.Single(new OpenLibrarySource(http).FindByIsbn("9781492034926", None));
        Assert.Equal("Kubernetes Patterns", book.Title);
        Assert.Equal(["Bilgin Ibryam", "Roland Huß"], book.Authors);
        Assert.Equal("O'Reilly Media", book.Publisher);
        Assert.Equal(2019, book.Year);
        Assert.Equal("en", book.Language);
        Assert.Equal("Patterns.", book.Description);
        Assert.Contains("bibkeys=ISBN:9781492034926", http.Urls[0], StringComparison.Ordinal);
    }

    [Fact]
    public void OpenLibrary_answers_an_unknown_ISBN_with_no_books() =>
        Assert.Empty(new OpenLibrarySource(new StubHttp("{}")).FindByIsbn("9781492034926", None));

    [Fact]
    public void OpenLibrary_search_reads_the_documents()
    {
        var http = new StubHttp("""
            { "docs": [ { "title": "Kubernetes Patterns", "author_name": ["Bilgin Ibryam"], "publisher": ["O'Reilly", "Other"],
                "first_publish_year": 2019, "language": ["eng", "ger"], "isbn": ["1492050288", "9781492050285"], "cover_i": 42 },
              { "author_name": ["No Title"] } ] }
            """);
        var book = Assert.Single(new OpenLibrarySource(http).Search("Kubernetes Patterns", "Bilgin Ibryam", None));
        Assert.Equal("9781492050285", book.Isbn13);
        Assert.Equal(2019, book.Year);
        Assert.Equal("en", book.Language);
        Assert.Equal("https://covers.openlibrary.org/b/id/42-L.jpg?default=false", book.CoverUrl);
        Assert.Contains("title=Kubernetes%20Patterns", http.Urls[0], StringComparison.Ordinal);
        Assert.Contains("author=Bilgin%20Ibryam", http.Urls[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Google_reads_a_volume()
    {
        var http = new StubHttp("""
            { "items": [ { "volumeInfo": {
                "title": "Kubernetes Patterns", "authors": ["Bilgin Ibryam"], "publisher": "O'Reilly Media",
                "publishedDate": "2019-04-02", "description": "Patterns.", "language": "en",
                "industryIdentifiers": [{ "type": "ISBN_10", "identifier": "1492050288" }, { "type": "ISBN_13", "identifier": "9781492050285" }],
                "imageLinks": { "thumbnail": "http://books.google.com/x?zoom=1" } } } ] }
            """);
        var book = Assert.Single(new GoogleBooksSource(http).FindByIsbn("9781492050285", None));
        Assert.Equal("9781492050285", book.Isbn13);
        Assert.Equal(2019, book.Year);
        Assert.Equal("https://books.google.com/x?zoom=1", book.CoverUrl);
        Assert.Contains("q=isbn%3A9781492050285", http.Urls[0], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, "HTTP 429 Too Many Requests")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "HTTP 503 Service Unavailable")]
    public void An_error_status_is_reported_with_its_reason(HttpStatusCode status, string message)
    {
        var e = Assert.Throws<MetadataSourceException>(() => new OpenLibrarySource(new StubHttp("", status)).FindByIsbn("9781492034926", None));
        Assert.Equal(message, e.Message);
    }

    [Fact]
    public void A_body_that_is_not_JSON_is_reported() =>
        Assert.Throws<MetadataSourceException>(() => new GoogleBooksSource(new StubHttp("<html>")).Search("x", null, None));

    [Fact]
    public void A_missing_cover_is_no_cover()
    {
        var book = new OnlineBook("openlibrary", null, "T", [], CoverUrl: "https://example.invalid/c.jpg");
        Assert.Null(new OpenLibrarySource(new StubHttp("", HttpStatusCode.NotFound)).FetchCover(book, None));
    }

    [Fact]
    public void A_cover_keeps_the_type_of_the_server()
    {
        var book = new OnlineBook("openlibrary", null, "T", [], CoverUrl: "https://example.invalid/c.png");
        var cover = new OpenLibrarySource(new StubHttp("PNG", contentType: "image/png")).FetchCover(book, None);
        Assert.Equal("image/png", cover?.ContentType);
    }

    private sealed class StubHttp(string body, HttpStatusCode status = HttpStatusCode.OK, string contentType = "application/json") : IHttpGetter
    {
        public List<string> Urls { get; } = [];

        public HttpAnswer Get(string url, CancellationToken cancellation)
        {
            Urls.Add(url);
            return new HttpAnswer(status, contentType, Encoding.UTF8.GetBytes(body));
        }
    }
}
