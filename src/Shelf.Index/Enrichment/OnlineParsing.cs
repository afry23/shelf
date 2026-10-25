using System.Globalization;
using System.Net;
using System.Text.Json;
using Shelf.Core.Model;

namespace Shelf.Index;

/// <summary>What the parsers of the online sources have in common.</summary>
internal static class OnlineParsing
{
    private static readonly Dictionary<string, string> Languages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["eng"] = "en", ["ger"] = "de", ["deu"] = "de", ["fre"] = "fr", ["fra"] = "fr", ["spa"] = "es", ["ita"] = "it",
        ["por"] = "pt", ["dut"] = "nl", ["nld"] = "nl", ["rus"] = "ru", ["lat"] = "la", ["jpn"] = "ja", ["chi"] = "zh", ["zho"] = "zh",
    };

    public static JsonDocument Parse(HttpAnswer answer, string what)
    {
        if (answer.Status != HttpStatusCode.OK)
        {
            throw new MetadataSourceException($"HTTP {(int)answer.Status} {Reason(answer.Status)}");
        }

        try
        {
            return JsonDocument.Parse(answer.Body);
        }
        catch (JsonException e)
        {
            throw new MetadataSourceException($"the answer to the {what} is not JSON", e);
        }
    }

    /// <summary>"HTTP 429 Too Many Requests", not "TooManyRequests".</summary>
    private static string Reason(HttpStatusCode status) => status switch
    {
        HttpStatusCode.TooManyRequests => "Too Many Requests",
        HttpStatusCode.NotFound => "Not Found",
        HttpStatusCode.Forbidden => "Forbidden",
        HttpStatusCode.InternalServerError => "Internal Server Error",
        HttpStatusCode.ServiceUnavailable => "Service Unavailable",
        _ => status.ToString(),
    };

    public static string? Text(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.String => Clean(value.GetString()),
                JsonValueKind.Object when value.TryGetProperty("value", out var inner) && inner.ValueKind == JsonValueKind.String => Clean(inner.GetString()),
                _ => null,
            }
            : null;

    public static IEnumerable<JsonElement> Items(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray()
            : [];

    public static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    /// <summary>The first four-digit year in a date such as "2019", "2019-12-03" or "March 2020".</summary>
    public static int? Year(string? date)
    {
        if (date is null)
        {
            return null;
        }

        for (var i = 0; i + 4 <= date.Length; i++)
        {
            var span = date.AsSpan(i, 4);
            var alone = (i == 0 || !char.IsAsciiDigit(date[i - 1])) && (i + 4 == date.Length || !char.IsAsciiDigit(date[i + 4]));
            if (alone && !span.ContainsAnyExceptInRange('0', '9') && int.Parse(span, NumberStyles.None, CultureInfo.InvariantCulture) > 0)
            {
                return int.Parse(span, NumberStyles.None, CultureInfo.InvariantCulture);
            }
        }

        return null;
    }

    /// <summary>"eng" or "/languages/eng" as "en"; a language shelf does not know is left out.</summary>
    public static string? Language(string? code)
    {
        if (code is null)
        {
            return null;
        }

        var last = code[(code.LastIndexOf('/') + 1)..];
        return last.Length == 2 ? last.ToLowerInvariant() : Languages.GetValueOrDefault(last);
    }

    public static string? FirstIsbn13(IEnumerable<string?> identifiers)
    {
        foreach (var identifier in identifiers)
        {
            if (identifier is not null && Isbn.FromIdentifier(identifier) is { Length: 13 } isbn)
            {
                return isbn;
            }
        }

        return null;
    }

    public static OnlineCover? Cover(IHttpGetter http, string? url, CancellationToken cancellation)
    {
        if (url is null)
        {
            return null;
        }

        var answer = http.Get(url, cancellation);
        // A missing cover is an answer, not a failure: Open Library says 404 for ?default=false.
        return answer.Status == HttpStatusCode.OK && answer.ContentType is { } type && type.StartsWith("image/", StringComparison.Ordinal) && answer.Body.Length > 0
            ? new OnlineCover(answer.Body, type)
            : null;
    }
}
