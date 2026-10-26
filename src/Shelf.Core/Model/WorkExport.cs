using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Shelf.Core.Organization;

namespace Shelf.Core.Model;

/// <summary>
/// A work as one Markdown file: the metadata as YAML front matter, then the highlights and notes (F-ANN-7). The file is
/// named after the cite key of the work.
/// </summary>
public static partial class WorkExport
{
    public const string MissingCiteKey = "the work has no cite key; set or generate a cite key first";

    /// <summary>The file name for the export, the cite key made safe as a file name.</summary>
    /// <exception cref="InvalidOperationException">The work has no cite key.</exception>
    public static string FileName(WorkSidecar work)
    {
        ArgumentNullException.ThrowIfNull(work);
        return string.IsNullOrWhiteSpace(work.CiteKey)
            ? throw new InvalidOperationException(MissingCiteKey)
            : PathSanitizer.Segment(work.CiteKey) + ".md";
    }

    /// <summary>The Markdown text; line endings are LF and the text ends with one.</summary>
    /// <exception cref="InvalidOperationException">The work has no cite key.</exception>
    public static string Render(WorkSidecar work, IReadOnlyList<Annotation> annotations)
    {
        ArgumentNullException.ThrowIfNull(work);
        ArgumentNullException.ThrowIfNull(annotations);
        if (string.IsNullOrWhiteSpace(work.CiteKey))
        {
            throw new InvalidOperationException(MissingCiteKey);
        }

        var text = new StringBuilder();
        text.Append("---\n");
        Field(text, "citekey", Scalar(work.CiteKey));
        Field(text, "title", Quoted(work.Title));
        Field(text, "subtitle", work.Subtitle is { Length: > 0 } subtitle ? Quoted(subtitle) : null);
        List(text, "authors", work.Authors);
        Field(text, "year", work.Year?.ToString(CultureInfo.InvariantCulture));
        Field(text, "language", Optional(work.Language));
        Field(text, "isbn10", Optional(work.Isbn10));
        Field(text, "isbn13", Optional(work.Isbn13));
        Field(text, "publisher", Optional(work.Publisher));
        Field(text, "series", Optional(work.Series));
        Field(text, "series_index", work.SeriesIndex?.ToString(CultureInfo.InvariantCulture));
        Field(text, "edition", Optional(work.Edition));
        List(text, "tags", work.Tags);
        Field(text, "rating", work.Rating?.ToString(CultureInfo.InvariantCulture));
        Field(text, "read_status", work.ReadStatus.ToString().ToLowerInvariant());
        Field(text, "id", work.Id.ToString());
        text.Append("---\n\n# ").Append(OneLine(work.Title)).Append('\n');
        if (!string.IsNullOrWhiteSpace(work.Description))
        {
            text.Append('\n').Append(Lf(work.Description).Trim()).Append('\n');
        }

        if (annotations.Count == 0)
        {
            text.Append("\nNo highlights or notes.\n");
            return text.ToString();
        }

        var highlights = annotations.Where(a => a.Kind == AnnotationKind.Highlight).ToList();
        var notes = annotations.Where(a => a.Kind == AnnotationKind.Note).ToList();
        Section(text, "Highlights", highlights);
        Section(text, "Notes", notes);
        return text.ToString();
    }

    private static void Section(StringBuilder text, string heading, List<Annotation> items)
    {
        if (items.Count == 0)
        {
            return;
        }

        text.Append("\n## ").Append(heading).Append('\n');
        var files = items.Select(a => a.File).Distinct(StringComparer.Ordinal).ToList();
        foreach (var file in files)
        {
            // The order within a file is the order of the source (oldest first).
            text.Append("\n### ").Append(OneLine(file)).Append('\n');
            foreach (var a in items.Where(x => x.File == file))
            {
                text.Append('\n');
                if (a.Quote is { } quote)
                {
                    foreach (var line in Lf(quote.Exact).Split('\n'))
                    {
                        text.Append("> ").Append(line).Append('\n');
                    }

                    text.Append('\n');
                    var place = Locators.Describe(a.Locator ?? string.Empty);
                    text.Append('*').Append(place).Append(a.Color is { Length: > 0 } color ? ", " + color : string.Empty).Append("*\n");
                }

                if (a.Note is { Length: > 0 } note)
                {
                    if (a.Quote is not null)
                    {
                        text.Append('\n');
                    }

                    text.Append(Lf(note).TrimEnd()).Append('\n');
                }
            }
        }
    }

    private static void Field(StringBuilder text, string name, string? value)
    {
        if (value is not null)
        {
            text.Append(name).Append(": ").Append(value).Append('\n');
        }
    }

    private static void List(StringBuilder text, string name, IReadOnlyList<string> values)
    {
        if (values.Count == 0)
        {
            return;
        }

        text.Append(name).Append(":\n");
        foreach (var value in values)
        {
            text.Append("  - ").Append(Scalar(value)).Append('\n');
        }
    }

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : Scalar(value);

    /// <summary>The text as a plain YAML scalar if that is safe, else in double quotes.</summary>
    private static string Scalar(string value) => PlainScalar().IsMatch(value) ? value : Quoted(value);

    /// <summary>A double-quoted YAML scalar (the JSON escapes are valid YAML).</summary>
    private static string Quoted(string value)
    {
        var quoted = new StringBuilder("\"");
        foreach (var c in value)
        {
            switch (c)
            {
                case '"': quoted.Append("\\\""); break;
                case '\\': quoted.Append("\\\\"); break;
                case '\n': quoted.Append("\\n"); break;
                case '\r': quoted.Append("\\r"); break;
                case '\t': quoted.Append("\\t"); break;
                default:
                    if (char.IsControl(c))
                    {
                        quoted.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        quoted.Append(c);
                    }

                    break;
            }
        }

        return quoted.Append('"').ToString();
    }

    private static string Lf(string value) => value.ReplaceLineEndings("\n");

    private static string OneLine(string value) => string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    // Starts with a letter or digit, no colon, hash or quote, and is not a word YAML reads as something else.
    [GeneratedRegex(@"^(?!(?i:true|false|yes|no|on|off|null|y|n)$)[\p{L}\p{N}][\p{L}\p{N} _./+\-]*$")]
    private static partial Regex PlainScalar();
}
