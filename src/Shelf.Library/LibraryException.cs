namespace Shelf.Library;

/// <summary>An error the caller can act on: a bad query, an unknown work, no text at a location. The message is meant to be shown.</summary>
public sealed class LibraryException(string message) : Exception(message);
