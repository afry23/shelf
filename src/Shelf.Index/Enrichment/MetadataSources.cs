using Shelf.Core.Configuration;

namespace Shelf.Index;

/// <summary>What an online source knows about one book or paper.</summary>
public sealed record OnlineBook(
    string Source,
    string? Isbn13,
    string Title,
    IReadOnlyList<string> Authors,
    string? Subtitle = null,
    string? Publisher = null,
    int? Year = null,
    string? Language = null,
    string? Description = null,
    string? CoverUrl = null,
    string? Doi = null,
    string? ArxivId = null,
    string? Venue = null,
    string? Abstract = null,
    string? Volume = null,
    string? Issue = null,
    string? Pages = null);

/// <summary>The bytes of a cover image and the type that the server named for them.</summary>
public sealed record OnlineCover(byte[] Bytes, string ContentType);

/// <summary>The source cannot be reached or answered with something shelf cannot use. The message is shown to the user as is.</summary>
public sealed class MetadataSourceException(string message, Exception? innerException = null) : Exception(message, innerException);

/// <summary>An online catalog of books (F-IMP-6). shelf asks it only on <c>shelf enrich</c>.</summary>
public interface IMetadataSource
{
    /// <summary>The name that <c>--source</c> takes: <c>openlibrary</c>, <c>google</c>, <c>crossref</c> or <c>arxiv</c>.</summary>
    string Name { get; }

    /// <summary>True for a catalog of papers (asked for papers only), false for a catalog of books (asked for books only), F-PAP-3.</summary>
    bool ForPapers => false;

    /// <summary>
    /// The source as the settings want it, or null if the settings leave it out (Unpaywall without an e-mail address, F-PAP-8).
    /// </summary>
    IMetadataSource? ForSettings(FetchSettings settings) => this;

    /// <exception cref="MetadataSourceException"/>
    IReadOnlyList<OnlineBook> FindByDoi(string doi, CancellationToken cancellation) => [];

    /// <exception cref="MetadataSourceException"/>
    IReadOnlyList<OnlineBook> FindByArxivId(string arxivId, CancellationToken cancellation) => [];

    /// <exception cref="MetadataSourceException"/>
    IReadOnlyList<OnlineBook> FindByIsbn(string isbn13, CancellationToken cancellation);

    /// <exception cref="MetadataSourceException"/>
    IReadOnlyList<OnlineBook> Search(string title, string? author, CancellationToken cancellation);

    /// <returns>The open-access PDF of the paper, or null if the source has none (F-PAP-8).</returns>
    /// <exception cref="MetadataSourceException"/>
    byte[]? FetchPdf(OnlineBook paper, CancellationToken cancellation) => null;

    /// <returns>The cover, or null if the source has none for the book.</returns>
    /// <exception cref="MetadataSourceException"/>
    OnlineCover? FetchCover(OnlineBook book, CancellationToken cancellation);
}
