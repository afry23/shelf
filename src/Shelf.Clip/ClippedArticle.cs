namespace Shelf.Clip;

/// <summary>An image of an article, saved under <c>images/</c> next to the Markdown file (F-ART-5).</summary>
public sealed record ClippedImage(string FileName, byte[] Bytes);

/// <summary>
/// An article read from a Markdown file that a web clipper saved (F-ART-3): the Markdown with links to
/// <c>images/&lt;file&gt;</c>, the images that were taken over, and what the front matter says about it.
/// </summary>
/// <param name="SourceUrl">The address without fragment and tracking parameters, or null if the file does not tell.</param>
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
