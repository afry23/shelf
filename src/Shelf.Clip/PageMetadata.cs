using System.Globalization;
using System.Text.Json;
using AngleSharp.Dom;

namespace Shelf.Clip;

/// <summary>What a page says about its article (F-ART-4): Open Graph, JSON-LD and plain <c>&lt;meta&gt;</c> tags.</summary>
internal sealed record PageMetadata(
    string? Title,
    IReadOnlyList<string> Authors,
    string? Site,
    DateTimeOffset? Published,
    string? Canonical,
    string? Language)
{
    public static PageMetadata Read(IDocument document)
    {
        var graph = JsonLd(document).ToList();

        string? Meta(params string[] names)
        {
            foreach (var name in names)
            {
                var content = document.QuerySelectorAll("meta")
                    .FirstOrDefault(m => string.Equals(m.GetAttribute("property"), name, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(m.GetAttribute("name"), name, StringComparison.OrdinalIgnoreCase))
                    ?.GetAttribute("content");
                if (!string.IsNullOrWhiteSpace(content))
                {
                    return content.Trim();
                }
            }

            return null;
        }

        string? Ld(params string[] path) => graph.Select(node => Walk(node, path)).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

        var title = Meta("og:title", "twitter:title") ?? Ld("headline")
            ?? Clean(document.QuerySelector("h1")?.TextContent)
            ?? Clean(document.Title);
        var site = Meta("og:site_name", "application-name") ?? Ld("publisher", "name") ?? Ld("isPartOf", "name");
        var published = Instant(Meta("article:published_time", "og:article:published_time", "date", "dc.date", "dcterms.date")
            ?? Ld("datePublished") ?? document.QuerySelector("time[datetime]")?.GetAttribute("datetime"));
        var canonical = document.QuerySelector("link[rel~=canonical]")?.GetAttribute("href") ?? Meta("og:url");

        var authors = new List<string>();
        foreach (var node in graph)
        {
            authors.AddRange(Names(node, "author"));
        }

        if (authors.Count == 0)
        {
            var meta = Meta("author", "article:author", "dc.creator");
            if (meta is not null && !meta.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                authors.Add(meta);
            }
        }

        if (authors.Count == 0 && Clean(document.QuerySelector("[rel~=author]")?.TextContent) is { } linked)
        {
            authors.Add(linked);
        }

        var language = document.DocumentElement.GetAttribute("lang");
        return new PageMetadata(
            title,
            [.. authors.Select(CleanName).Where(n => n.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase)],
            site,
            published,
            canonical,
            string.IsNullOrWhiteSpace(language) ? null : language.Trim());
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string CleanName(string name)
    {
        var cleaned = Clean(name) ?? string.Empty;
        return cleaned.StartsWith("by ", StringComparison.OrdinalIgnoreCase) ? cleaned[3..].Trim() : cleaned;
    }

    private static DateTimeOffset? Instant(string? text) =>
        text is not null && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value)
            ? value.ToUniversalTime()
            : null;

    private static IEnumerable<JsonElement> JsonLd(IDocument document)
    {
        foreach (var script in document.QuerySelectorAll("script[type=\"application/ld+json\"]"))
        {
            JsonDocument parsed;
            try
            {
                parsed = JsonDocument.Parse(script.TextContent);
            }
            catch (JsonException)
            {
                continue;
            }

            foreach (var node in Flatten(parsed.RootElement.Clone()))
            {
                yield return node;
            }
        }
    }

    private static IEnumerable<JsonElement> Flatten(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray().SelectMany(Flatten))
            {
                yield return item;
            }
        }
        else if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("@graph", out var graph))
            {
                foreach (var item in Flatten(graph))
                {
                    yield return item;
                }
            }

            yield return element;
        }
    }

    private static string? Walk(JsonElement node, string[] path)
    {
        var current = node;
        foreach (var step in path)
        {
            if (current.ValueKind == JsonValueKind.Array)
            {
                current = current.EnumerateArray().FirstOrDefault();
            }

            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(step, out current))
            {
                return null;
            }
        }

        return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
    }

    private static IEnumerable<string> Names(JsonElement node, string property)
    {
        if (node.ValueKind != JsonValueKind.Object || !node.TryGetProperty(property, out var value))
        {
            yield break;
        }

        foreach (var item in value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().ToList() : [value])
        {
            if (item.ValueKind == JsonValueKind.String && item.GetString() is { } name)
            {
                yield return name;
            }
            else if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("name", out var named) && named.ValueKind == JsonValueKind.String)
            {
                yield return named.GetString()!;
            }
        }
    }
}
