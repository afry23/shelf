using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Shelf.Core.Model;

namespace Shelf.Library;

/// <summary>
/// One field of a JSON merge patch as an edit of a sidecar. The rules are those of <c>shelf edit</c>: a title that is
/// not empty, a year from 1 to 9999, a rating from 1 to 5, a valid ISBN, a known reading status. A field set to
/// <c>null</c> (or to empty text) is removed, where the field is optional.
/// </summary>
internal static class MetadataPatch
{
    private static readonly string[] Statuses = ["unread", "reading", "read"];

    /// <exception cref="LibraryException">The field is unknown or its value is not valid.</exception>
    public static Func<WorkSidecar, WorkSidecar> Edit(string field, JsonNode? value)
    {
        switch (field)
        {
            case "title":
                var title = Text(field, value)?.Trim();
                if (string.IsNullOrEmpty(title))
                {
                    throw Refuse(field, "must not be empty");
                }

                return s => s with { Title = title };
            case "subtitle":
                var subtitle = Optional(field, value);
                return s => s with { Subtitle = subtitle };
            case "language":
                var language = Optional(field, value);
                return s => s with { Language = language };
            case "publisher":
                var publisher = Optional(field, value);
                return s => s with { Publisher = publisher };
            case "series":
                var series = Optional(field, value);
                return s => s with { Series = series };
            case "edition":
                var edition = Optional(field, value);
                return s => s with { Edition = edition };
            case "citeKey":
                var citeKey = Optional(field, value);
                if (citeKey is not null && !CiteKeys.IsValid(citeKey))
                {
                    throw Refuse(field, CiteKeys.Rule);
                }

                return s => s with { CiteKey = citeKey };
            case "description":
                var description = Optional(field, value);
                return s => s with { Description = description };
            case "authors":
                var authors = Authors(value);
                return s => s with { Authors = authors };
            case "year":
                var year = Whole(field, value, 1, 9999);
                return s => s with { Year = year };
            case "rating":
                var rating = Whole(field, value, 1, 5);
                return s => s with { Rating = rating };
            case "seriesIndex":
                var index = SeriesIndex(value);
                return s => s with { SeriesIndex = index };
            case "isbn":
                return Isbn(value);
            case "readStatus":
                var status = Text(field, value)?.Trim().ToLowerInvariant();
                if (status is null || !Statuses.Contains(status))
                {
                    throw Refuse(field, "must be unread, reading or read");
                }

                var parsed = Enum.Parse<ReadStatus>(status, ignoreCase: true);
                return s => s with { ReadStatus = parsed };
            default:
                throw Refuse(field, "unknown field");
        }
    }

    private static LibraryException Refuse(string field, string message) => new($"{field}: {message}");

    /// <summary>Text, or null for JSON null. Anything else is refused.</summary>
    private static string? Text(string field, JsonNode? value)
    {
        if (value is null)
        {
            return null;
        }

        return value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : throw Refuse(field, "must be text");
    }

    /// <summary>Optional text: null and empty text remove the field.</summary>
    private static string? Optional(string field, JsonNode? value) =>
        Text(field, value) is { } text && text.Trim().Length > 0 ? text.Trim() : null;

    private static List<string> Authors(JsonNode? value)
    {
        if (value is not JsonArray array || array.Count == 0)
        {
            throw Refuse("authors", "give at least one author");
        }

        var names = new List<string>();
        foreach (var item in array)
        {
            var name = item is not null && item.GetValueKind() == JsonValueKind.String ? item.GetValue<string>().Trim() : null;
            if (string.IsNullOrEmpty(name))
            {
                throw Refuse("authors", "must not be empty");
            }

            names.Add(name);
        }

        return names;
    }

    private static int? Whole(string field, JsonNode? value, int min, int max)
    {
        if (value is null)
        {
            return null;
        }

        if (value.GetValueKind() != JsonValueKind.Number || !value.AsValue().TryGetValue<int>(out var number))
        {
            throw Refuse(field, "must be a whole number");
        }

        return number >= min && number <= max ? number : throw Refuse(field, $"must be between {min.ToString(CultureInfo.InvariantCulture)} and {max.ToString(CultureInfo.InvariantCulture)}");
    }

    private static double? SeriesIndex(JsonNode? value)
    {
        if (value is null)
        {
            return null;
        }

        return value.GetValueKind() == JsonValueKind.Number && value.AsValue().TryGetValue<double>(out var number)
            ? number
            : throw Refuse("seriesIndex", "must be a number");
    }

    private static Func<WorkSidecar, WorkSidecar> Isbn(JsonNode? value)
    {
        var text = Text("isbn", value)?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return s => s with { Isbn10 = null, Isbn13 = null };
        }

        var digits = new string([.. text.Where(c => c is not ('-' or ' '))]).ToUpperInvariant();
        if (digits.Length == 13 && Core.Model.Isbn.IsValid13(digits))
        {
            return s => s with { Isbn10 = null, Isbn13 = digits };
        }

        if (digits.Length == 10 && Core.Model.Isbn.IsValid10(digits))
        {
            return s => s with { Isbn10 = digits, Isbn13 = Core.Model.Isbn.ToIsbn13(digits) };
        }

        throw Refuse("isbn", $"'{text}' is not a valid ISBN");
    }
}
