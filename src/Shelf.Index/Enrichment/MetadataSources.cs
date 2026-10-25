namespace Shelf.Index;

/// <summary>What an online source knows about one book.</summary>
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
    string? CoverUrl = null);

/// <summary>The bytes of a cover image and the type that the server named for them.</summary>
public sealed record OnlineCover(byte[] Bytes, string ContentType);

/// <summary>The source cannot be reached or answered with something shelf cannot use. The message is shown to the user as is.</summary>
public sealed class MetadataSourceException(string message, Exception? innerException = null) : Exception(message, innerException);

/// <summary>An online catalog of books (F-IMP-6). shelf asks it only on <c>shelf enrich</c>.</summary>
public interface IMetadataSource
{
    /// <summary>The name that <c>--source</c> takes: <c>openlibrary</c> or <c>google</c>.</summary>
    string Name { get; }

    /// <exception cref="MetadataSourceException"/>
    IReadOnlyList<OnlineBook> FindByIsbn(string isbn13, CancellationToken cancellation);

    /// <exception cref="MetadataSourceException"/>
    IReadOnlyList<OnlineBook> Search(string title, string? author, CancellationToken cancellation);

    /// <returns>The cover, or null if the source has none for the book.</returns>
    /// <exception cref="MetadataSourceException"/>
    OnlineCover? FetchCover(OnlineBook book, CancellationToken cancellation);
}
