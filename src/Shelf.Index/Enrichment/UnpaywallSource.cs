using System.Net;
using System.Text.Json;

namespace Shelf.Index;

/// <summary>
/// Unpaywall (https://api.unpaywall.org): where a paper with a DOI can be read for free, legally. It knows no metadata; it only
/// finds the PDF of a paper that <c>shelf fetch</c> has the DOI of (F-PAP-8). Every request carries the e-mail address that
/// the service asks for; without one the source is not used.
/// </summary>
public sealed class UnpaywallSource(IHttpGetter http, string? email) : IMetadataSource
{
    // A paper can be at many places; a few tries are enough, and more would only be more traffic for a service that is free.
    private const int MaxTries = 3;

    public string Name => "unpaywall";

    public bool ForPapers => true;

    public IMetadataSource? ForSettings(Shelf.Core.Configuration.FetchSettings settings) =>
        settings.UnpaywallEmail is { } address ? new UnpaywallSource(http, address) : null;

    public IReadOnlyList<OnlineBook> FindByIsbn(string isbn13, CancellationToken cancellation) => [];

    public IReadOnlyList<OnlineBook> Search(string title, string? author, CancellationToken cancellation) => [];

    public byte[]? FetchPdf(OnlineBook paper, CancellationToken cancellation)
    {
        if (email is null || paper.Doi is not { } doi)
        {
            return null;
        }

        var path = string.Join('/', doi.Split('/').Select(Uri.EscapeDataString));
        var answer = http.Get($"https://api.unpaywall.org/v2/{path}?email={Uri.EscapeDataString(email)}", cancellation);
        if (answer.Status == HttpStatusCode.NotFound)
        {
            return null;
        }

        using var document = OnlineParsing.Parse(answer, "DOI lookup");
        foreach (var url in PdfUrls(document.RootElement).Take(MaxTries))
        {
            try
            {
                var file = http.Get(url, cancellation);

                // A repository may answer with a page about the paper instead of the paper.
                if (file.Status == HttpStatusCode.OK && file.Body.AsSpan().StartsWith("%PDF"u8))
                {
                    return file.Body;
                }
            }
            catch (MetadataSourceException)
            {
                // One bad copy is no reason not to try the next.
            }
        }

        return null;
    }

    public OnlineCover? FetchCover(OnlineBook book, CancellationToken cancellation) => null;

    // The best copy first, then the others; only copies that have a link to the PDF itself.
    private static IEnumerable<string> PdfUrls(JsonElement root)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        if (root.TryGetProperty("best_oa_location", out var best) && OnlineParsing.Text(best, "url_for_pdf") is { } first && seen.Add(first))
        {
            yield return first;
        }

        foreach (var location in OnlineParsing.Items(root, "oa_locations"))
        {
            if (OnlineParsing.Text(location, "url_for_pdf") is { } url && seen.Add(url))
            {
                yield return url;
            }
        }
    }
}
