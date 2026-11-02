using System.Globalization;
using System.Text;

namespace Shelf.Core.Model;

/// <summary>
/// The highlights and notes of works as one CSV in the layout of the Readwise import (F-ANN-9): one row per highlight and per
/// note, no bookmarks. Fields with a comma, a quotation mark or a line break are quoted; lines end with LF.
/// </summary>
public static class ReadwiseCsv
{
    public const string Header = "Highlight,Title,Author,URL,Note,Location,Location Type,Date";

    public static string Render(IEnumerable<(WorkSidecar Work, IReadOnlyList<Annotation> Annotations)> works)
    {
        ArgumentNullException.ThrowIfNull(works);
        var text = new StringBuilder();
        text.Append(Header).Append('\n');
        foreach (var (work, annotations) in works)
        {
            foreach (var a in annotations.Where(x => x.Kind != AnnotationKind.Bookmark))
            {
                var highlight = a.Kind == AnnotationKind.Highlight ? a.Quote?.Exact : null;
                if (string.IsNullOrWhiteSpace(highlight) && string.IsNullOrWhiteSpace(a.Note))
                {
                    continue;
                }

                var page = Locators.TryParsePdfPage(a.Locator, out var number);
                Row(
                    text,
                    highlight,
                    work.Title,
                    string.Join(", ", work.Authors),
                    string.Empty,
                    a.Note,
                    page ? number.ToString(CultureInfo.InvariantCulture) : string.Empty,
                    page ? "page" : string.Empty,
                    a.CreatedAt.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            }
        }

        return text.ToString();
    }

    private static void Row(StringBuilder text, params string?[] fields)
    {
        for (var i = 0; i < fields.Length; i++)
        {
            if (i > 0)
            {
                text.Append(',');
            }

            text.Append(Field(fields[i]));
        }

        text.Append('\n');
    }

    private static string Field(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.AsSpan().IndexOfAny(",\"\r\n") < 0 ? value : "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
