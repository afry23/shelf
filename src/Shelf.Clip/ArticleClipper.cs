using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using ReverseMarkdown;
using SmartReader;

namespace Shelf.Clip;

/// <summary>
/// Turns the HTML of a page into an article (F-ART-4, F-ART-5): the main content as Markdown, the images that belong to it,
/// and the metadata of the page. Nothing here touches the network or the library; where an image comes from is the
/// business of the <c>load</c> function.
/// </summary>
public static partial class ArticleClipper
{
    /// <summary>Images smaller than this many pixels (icons, tracking pixels) are not part of an article.</summary>
    internal const int MinimumPixels = 10_000;

    /// <summary>
    /// Less text than this is what is left of a page that has no article: SmartReader then falls back to the text of the whole
    /// page (a menu, an imprint).
    /// </summary>
    internal const int MinimumTextLength = 80;

    // Relative addresses are resolved against this host so that SmartReader gets an absolute address; the prefix is cut off again.
    private const string Placeholder = "http://shelf.invalid/";

    private static readonly string[] LazyAttributes = ["data-src", "data-original", "data-lazy-src", "data-lazy", "data-full-url"];

    /// <param name="html">The page.</param>
    /// <param name="pageUrl">The address the page was loaded from, or null for a page saved in a browser.</param>
    /// <param name="load">
    /// The bytes of an image by the address it has in the page (relative to the page, or absolute), or null if it cannot be
    /// had. Called once for every address.
    /// </param>
    /// <exception cref="NoArticleException">The page holds no article.</exception>
    public static ClippedArticle Clip(string html, string? pageUrl, Func<string, byte[]?> load)
    {
        ArgumentNullException.ThrowIfNull(html);
        ArgumentNullException.ThrowIfNull(load);

        var document = new HtmlParser().ParseDocument(html);
        var page = PageMetadata.Read(document);
        var baseUrl = SourceUrl.Normalize(pageUrl) is not null ? pageUrl! : Placeholder;
        PrepareImages(document, new Uri(baseUrl));
        RemoveFurniture(document);

        SmartReader.Article article;
        try
        {
            article = new Reader(baseUrl, document.DocumentElement.OuterHtml).GetArticle();
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            throw new NoArticleException("the page could not be read: " + e.Message);
        }

        if (string.IsNullOrWhiteSpace(article.Content) || (Clean(article.TextContent)?.Length ?? 0) < MinimumTextLength)
        {
            throw new NoArticleException("no article found");
        }

        var converter = new Converter(new Config
        {
            GithubFlavored = true,
            Tags = { Unknown = Config.UnknownTagsOption.Bypass },
            Formatting = { RemoveComments = true },
        });
        var markdown = converter.Convert(article.Content);

        var title = Clean(page.Title) ?? Clean(article.Title) ?? "Untitled";
        var warnings = new List<string>();
        var (body, images) = SaveImages(markdown, load, warnings);
        var sourceUrl = SourceUrl.Normalize(page.Canonical is null ? null : Absolute(page.Canonical, baseUrl)) ?? SourceUrl.Normalize(pageUrl);
        var authors = page.Authors.Count > 0 ? page.Authors
            : Clean(article.Author) is { } byline ? [byline]
            : [];
        var site = Clean(page.Site) ?? Clean(article.SiteName) ?? HostOf(sourceUrl);
        return new ClippedArticle(
            title,
            authors,
            site,
            page.Published,
            sourceUrl,
            page.Language ?? Clean(article.Language),
            "# " + title + "\n\n" + body.Trim() + "\n",
            images,
            warnings);
    }

    /// <summary>
    /// Drops what is never the article: scripts, navigation, sidebars, footers, forms and comment sections. SmartReader scores
    /// the blocks of a page, but on a short page it keeps the rest of the body, so the obvious is taken out first.
    /// </summary>
    private static void RemoveFurniture(IDocument document)
    {
        foreach (var element in document.QuerySelectorAll(
            "script, style, noscript, nav, aside, footer, form, iframe, [role=navigation], [role=complementary], [role=contentinfo], #comments, #disqus_thread, .comments, .comment-list").ToList())
        {
            element.Remove();
        }
    }

    /// <summary>Gives every <c>img</c> the one address it is going to be loaded from: lazy-load attributes first, then the largest <c>srcset</c> entry.</summary>
    private static void PrepareImages(IDocument document, Uri baseUri)
    {
        foreach (var image in document.QuerySelectorAll("img").ToList())
        {
            var chosen = LazyAttributes.Select(image.GetAttribute).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))
                ?? Largest(image.GetAttribute("srcset") ?? image.GetAttribute("data-srcset"))
                ?? Largest(image.ParentElement is { LocalName: "picture" } picture
                    ? string.Join(',', picture.QuerySelectorAll("source").Select(s => s.GetAttribute("srcset")))
                    : null)
                ?? image.GetAttribute("src");
            foreach (var name in new[] { "srcset", "data-srcset", "sizes", "loading" }.Concat(LazyAttributes))
            {
                image.RemoveAttribute(name);
            }

            if (string.IsNullOrWhiteSpace(chosen) || chosen.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                image.Remove();
                continue;
            }

            image.SetAttribute("src", Resolve(baseUri, chosen.Trim()));
        }
    }

    /// <summary>The address with the greatest width (<c>1600w</c>) or density (<c>2x</c>) of a <c>srcset</c>; the last one if none says.</summary>
    private static string? Largest(string? srcset)
    {
        if (string.IsNullOrWhiteSpace(srcset))
        {
            return null;
        }

        string? best = null;
        var bestWeight = double.MinValue;
        foreach (var entry in srcset.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var pieces = entry.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (pieces.Length == 0)
            {
                continue;
            }

            var weight = pieces.Length > 1 && double.TryParse(pieces[1].TrimEnd('w', 'x'), System.Globalization.CultureInfo.InvariantCulture, out var w) ? w : 0;
            if (weight >= bestWeight)
            {
                best = pieces[0];
                bestWeight = weight;
            }
        }

        return best;
    }

    private static string Resolve(Uri baseUri, string address) =>
        Uri.TryCreate(baseUri, address, out var resolved) ? resolved.AbsoluteUri : address;

    private static string Absolute(string address, string baseUrl) => Resolve(new Uri(baseUrl), address);

    /// <summary>Loads the images of the Markdown, keeps the ones that belong to the article, and points their links to <c>images/</c>.</summary>
    private static (string Markdown, List<ClippedImage> Images) SaveImages(string markdown, Func<string, byte[]?> load, List<string> warnings)
    {
        var saved = new List<ClippedImage>();
        var byHash = new Dictionary<string, string>(StringComparer.Ordinal);
        var byAddress = new Dictionary<string, string?>(StringComparer.Ordinal); // null: dropped
        var result = ImageMarkup().Replace(markdown, match =>
        {
            var alt = match.Groups["alt"].Value;
            var address = match.Groups["src"].Value;
            if (!byAddress.TryGetValue(address, out var target))
            {
                target = Save(address, load, saved, byHash, warnings);
                byAddress[address] = target;
            }

            if (target is null)
            {
                return string.Empty;
            }

            return $"![{alt}]({target})";
        });
        return (MoreThanTwoBlankLines().Replace(result, "\n\n"), saved);
    }

    /// <returns>The link to put in the Markdown; null if the image does not belong to the article.</returns>
    private static string? Save(string address, Func<string, byte[]?> load, List<ClippedImage> saved, Dictionary<string, string> byHash, List<string> warnings)
    {
        var shown = address.StartsWith(Placeholder, StringComparison.Ordinal) ? Uri.UnescapeDataString(address[Placeholder.Length..]) : address;
        byte[]? bytes;
        try
        {
            bytes = load(shown);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            bytes = null;
        }

        if (bytes is null || ImageProbe.Read(bytes) is not { } probe)
        {
            warnings.Add("image not saved: " + shown + (bytes is null ? string.Empty : " (not an image)"));
            return shown;
        }

        if ((long)probe.Width * probe.Height < MinimumPixels)
        {
            return null;
        }

        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes))[..8];
        if (!byHash.TryGetValue(hash, out var name))
        {
            name = $"{saved.Count + 1:D2}-{hash}.{probe.Extension}";
            saved.Add(new ClippedImage(name, bytes));
            byHash[hash] = name;
        }

        return "images/" + name;
    }

    private static string? Clean(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string? HostOf(string? url) =>
        url is not null && Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host.StartsWith("www.", StringComparison.Ordinal) ? uri.Host[4..] : uri.Host : null;

    [GeneratedRegex("""!\[(?<alt>(?:[^\[\]]|\\.)*)\]\(<?(?<src>[^)\s>]+)>?(?:\s+"[^"]*")?\)""")]
    private static partial Regex ImageMarkup();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex MoreThanTwoBlankLines();
}
