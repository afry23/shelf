namespace Shelf.Library;

/// <summary>What went wrong, for callers that tell a missing thing from a request that cannot be met.</summary>
public enum LibraryErrorKind
{
    /// <summary>The request cannot be met as it is: a bad query, a value out of range.</summary>
    Invalid,

    /// <summary>The work or the text asked for does not exist.</summary>
    NotFound,
}

/// <summary>An error the caller can act on: a bad query, an unknown work, no text at a location. The message is meant to be shown.</summary>
public sealed class LibraryException(string message, LibraryErrorKind kind = LibraryErrorKind.Invalid) : Exception(message)
{
    public LibraryErrorKind Kind { get; } = kind;
}
