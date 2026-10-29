namespace Shelf.Clip;

/// <summary>An image of an article, saved under <c>images/</c> next to the Markdown file (F-ART-5).</summary>
public sealed record ClippedImage(string FileName, byte[] Bytes);

/// <summary>
/// An article taken from a web page (F-ART-4): the Markdown with links to <c>images/&lt;file&gt;</c>, the images that were
/// saved, and what the page says about itself.
/// </summary>
/// <param name="SourceUrl">The canonical address without fragment and tracking parameters, or null if the page does not tell.</param>
public sealed record ClippedArticle(
    string Title,
    IReadOnlyList<string> Authors,
    string? Site,
    DateTimeOffset? Published,
    string? SourceUrl,
    string? Language,
    string Markdown,
    IReadOnlyList<ClippedImage> Images,
    IReadOnlyList<string> Warnings);

/// <summary>The page is no article: nothing was left after the navigation, ads and comments were dropped.</summary>
public sealed class NoArticleException(string message) : Exception(message);
