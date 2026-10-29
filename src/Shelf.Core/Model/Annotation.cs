using System.Text.Json;

namespace Shelf.Core.Model;

/// <summary>What an annotation is: a marked passage, a note for the whole file, or a bookmark (F-ANN-1, F-ANN-2, F-RDR-6).</summary>
public enum AnnotationKind
{
    Highlight,
    Note,
    Bookmark,
}

/// <summary>The marked text with some text around it, so that a highlight can be found again if the location no longer fits.</summary>
public sealed record AnnotationQuote(string Exact, string? Prefix = null, string? Suffix = null);

/// <summary>A marked area of a page, as fractions of the page: from the left and top edge, and its width and height (0 to 1).</summary>
public sealed record AnnotationRect(double X, double Y, double Width, double Height);

/// <summary>
/// A highlight or a note of one file. A highlight has a locator (an EPUB CFI or <c>page=N</c>), a quote and a color;
/// a note has neither; a bookmark has a locator and nothing else, its note is the label. Highlights and notes can carry a note in Markdown. A highlight in a PDF also keeps the areas it marks on the page
/// (<see cref="Rects"/>), because a PDF page has no text positions that a locator could name.
/// </summary>
public sealed record Annotation(
    Ulid Id,
    string File,
    AnnotationKind Kind,
    string? Locator,
    AnnotationQuote? Quote,
    string? Color,
    string? Note,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<AnnotationRect>? Rects = null);

/// <summary>The content of an <c>annotations.json</c> next to a <c>book.json</c> (ADR 0018).</summary>
public sealed record AnnotationsSidecar
{
    public const int CurrentSchema = 1;

    public static IReadOnlyList<string> Colors { get; } = ["yellow", "green", "blue", "pink", "orange"];

    public const string DefaultColor = "yellow";

    public int Schema { get; init; } = CurrentSchema;

    public IReadOnlyList<Annotation> Annotations { get; init; } = [];

    public IReadOnlyDictionary<string, JsonElement> UnknownFields { get; init; } = EmptyUnknownFields;

    private static readonly IReadOnlyDictionary<string, JsonElement> EmptyUnknownFields = new Dictionary<string, JsonElement>();
}
