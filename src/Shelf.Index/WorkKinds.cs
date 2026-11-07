using Shelf.Core.Model;

namespace Shelf.Index;

/// <summary>The kind of a work as a person writes it (F-GRP-10): <c>book</c>, <c>paper</c> or <c>article</c>.</summary>
public static class WorkKinds
{
    /// <summary>Null or empty means that the file decides.</summary>
    /// <exception cref="LibraryEditException">The text names no kind.</exception>
    public static WorkKind? Parse(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null
        : text.Trim().ToLowerInvariant() switch
        {
            "book" => WorkKind.Book,
            "paper" => WorkKind.Paper,
            "article" => WorkKind.Article,
            _ => throw new LibraryEditException($"kind: '{text}' is not one of book, paper, article"),
        };
}
