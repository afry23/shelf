using System.Net;
using System.Text;
using Shelf.Core.Configuration;
using Xunit;

namespace Shelf.Index.Tests;

public sealed class UnpaywallSourceTests
{
    private static readonly CancellationToken None = CancellationToken.None;
    private static readonly byte[] Pdf = "%PDF-1.7\n"u8.ToArray();
    private static readonly OnlineBook Paper = new("crossref", null, "Resnet", [], Doi: "10.1109/cvpr.2016.90");

    [Fact]
    public void The_request_names_the_DOI_and_carries_the_address()
    {
        var http = new RoutedHttp { ["api.unpaywall.org"] = Json("""{ "is_oa": false }""") };
        Assert.Null(new UnpaywallSource(http, "me+shelf@example.org").FetchPdf(Paper, None));
        Assert.Equal(["https://api.unpaywall.org/v2/10.1109/cvpr.2016.90?email=me%2Bshelf%40example.org"], http.Urls);
    }

    [Fact]
    public void The_best_copy_is_taken_first()
    {
        var http = new RoutedHttp
        {
            ["api.unpaywall.org"] = Json("""
                { "best_oa_location": { "url_for_pdf": "https://a.example/best.pdf" },
                  "oa_locations": [ { "url_for_pdf": "https://a.example/best.pdf" }, { "url_for_pdf": "https://b.example/other.pdf" } ] }
                """),
            ["a.example"] = new HttpAnswer(HttpStatusCode.OK, "application/pdf", Pdf),
        };
        Assert.Equal(Pdf, new UnpaywallSource(http, "me@example.org").FetchPdf(Paper, None));
        Assert.DoesNotContain(http.Urls, u => u.Contains("b.example", StringComparison.Ordinal));
    }

    [Fact]
    public void A_copy_that_is_not_a_PDF_or_cannot_be_loaded_is_skipped()
    {
        var http = new RoutedHttp
        {
            ["api.unpaywall.org"] = Json("""
                { "best_oa_location": { "url_for_pdf": "https://html.example/p.pdf" },
                  "oa_locations": [ { "url_for_pdf": "https://html.example/p.pdf" }, { "url_for_pdf": "https://gone.example/p.pdf" }, { "url_for_pdf": "https://c.example/p.pdf" }, { "url_for_pdf": null } ] }
                """),
            ["html.example"] = new HttpAnswer(HttpStatusCode.OK, "text/html", "<html>landing page</html>"u8.ToArray()),
            ["gone.example"] = new HttpAnswer(HttpStatusCode.NotFound, null, []),
            ["c.example"] = new HttpAnswer(HttpStatusCode.OK, "application/pdf", Pdf),
        };
        Assert.Equal(Pdf, new UnpaywallSource(http, "me@example.org").FetchPdf(Paper, None));
    }

    [Fact]
    public void Only_a_few_copies_are_tried()
    {
        var locations = string.Join(",", Enumerable.Range(1, 6).Select(i => $$"""{ "url_for_pdf": "https://h{{i}}.example/p.pdf" }"""));
        var http = new RoutedHttp { ["api.unpaywall.org"] = Json($$"""{ "oa_locations": [ {{locations}} ] }""") };
        Assert.Null(new UnpaywallSource(http, "me@example.org").FetchPdf(Paper, None));
        Assert.Equal(1 + 3, http.Urls.Count);
    }

    [Fact]
    public void A_copy_that_throws_does_not_stop_the_next()
    {
        var http = new RoutedHttp
        {
            ["api.unpaywall.org"] = Json("""{ "oa_locations": [ { "url_for_pdf": "https://down.example/p.pdf" }, { "url_for_pdf": "https://c.example/p.pdf" } ] }"""),
            ["c.example"] = new HttpAnswer(HttpStatusCode.OK, "application/pdf", Pdf),
        };
        http.Throws.Add("down.example");
        Assert.Equal(Pdf, new UnpaywallSource(http, "me@example.org").FetchPdf(Paper, None));
    }

    [Fact]
    public void An_unknown_DOI_has_no_PDF()
    {
        var http = new RoutedHttp { ["api.unpaywall.org"] = new HttpAnswer(HttpStatusCode.NotFound, "application/json", []) };
        Assert.Null(new UnpaywallSource(http, "me@example.org").FetchPdf(Paper, None));
    }

    [Fact]
    public void A_refused_address_is_an_error_the_user_can_read()
    {
        var http = new RoutedHttp { ["api.unpaywall.org"] = new HttpAnswer(HttpStatusCode.UnprocessableEntity, "application/json", []) };
        var e = Assert.Throws<MetadataSourceException>(() => new UnpaywallSource(http, "me@example.org").FetchPdf(Paper, None));
        Assert.Contains("422", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Without_an_address_or_a_DOI_nothing_is_asked()
    {
        var http = new RoutedHttp();
        Assert.Null(new UnpaywallSource(http, null).FetchPdf(Paper, None));
        Assert.Null(new UnpaywallSource(http, "me@example.org").FetchPdf(Paper with { Doi = null }, None));
        Assert.Empty(http.Urls);
    }

    [Fact]
    public void The_settings_decide_whether_the_source_is_used()
    {
        var source = new UnpaywallSource(new RoutedHttp(), null);
        Assert.Null(source.ForSettings(FetchSettings.Default));
        Assert.NotNull(source.ForSettings(new FetchSettings("me@example.org")));
    }

    [Fact]
    public void It_knows_no_metadata()
    {
        IMetadataSource source = new UnpaywallSource(new RoutedHttp(), "me@example.org");
        Assert.Empty(source.FindByDoi("10.1109/cvpr.2016.90", None));
        Assert.Empty(source.Search("Resnet", null, None));
        Assert.True(source.ForPapers);
    }

    private static HttpAnswer Json(string body) => new(HttpStatusCode.OK, "application/json", Encoding.UTF8.GetBytes(body));

    // Answers by host name; a host that is not set is unreachable (404).
    private sealed class RoutedHttp : Dictionary<string, HttpAnswer>, IHttpGetter
    {
        public List<string> Urls { get; } = [];

        public HashSet<string> Throws { get; } = [];

        public HttpAnswer Get(string url, CancellationToken cancellation)
        {
            Urls.Add(url);
            var host = new Uri(url).Host;
            if (Throws.Contains(host))
            {
                throw new MetadataSourceException("connection refused");
            }

            return TryGetValue(host, out var answer) ? answer : new HttpAnswer(HttpStatusCode.NotFound, null, []);
        }
    }
}
