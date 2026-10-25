using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Shelf.Core.Model;

/// <summary>
/// Reads, validates and writes the JSON of an <c>annotations.json</c> (ADR 0018). Written by hand like
/// <see cref="SidecarFormat"/>: error messages name the key, the key order is fixed, no reflection (Native AOT).
/// </summary>
public static class AnnotationsFormat
{
    public const string FileName = "annotations.json";

    /// <summary>The longest locator that is accepted (the same bound as for a reading position).</summary>
    public const int MaxLocatorLength = 2000;

    /// <summary>The most areas one highlight may mark (a line of a page is one area).</summary>
    public const int MaxRects = 200;

    private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    private static readonly HashSet<string> KnownKeys = ["schema", "annotations"];

    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <exception cref="SidecarException"/>
    public static AnnotationsSidecar Parse(string text, string source)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(source);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text);
        }
        catch (JsonException e)
        {
            throw new SidecarException($"{source}: not valid JSON: {e.Message}", source, e);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new SidecarException($"{source}: not valid JSON: expected an object", source);
            }

            var schema = 1;
            if (root.TryGetProperty("schema", out var schemaElement))
            {
                if (schemaElement.ValueKind != JsonValueKind.Number || !schemaElement.TryGetInt32(out schema))
                {
                    throw new SidecarException($"{source}: schema: must be a whole number", source);
                }
            }

            if (schema > AnnotationsSidecar.CurrentSchema)
            {
                throw new SidecarException(
                    $"{source}: schema {schema} is newer than this version of shelf supports ({AnnotationsSidecar.CurrentSchema})",
                    source);
            }

            var list = new List<Annotation>();
            if (root.TryGetProperty("annotations", out var array))
            {
                if (array.ValueKind != JsonValueKind.Array)
                {
                    throw new SidecarException($"{source}: annotations: must be an array", source);
                }

                var i = 0;
                foreach (var item in array.EnumerateArray())
                {
                    list.Add(ReadAnnotation(item, $"annotations[{++i}]", source));
                }
            }

            var unknown = new Dictionary<string, JsonElement>();
            foreach (var property in root.EnumerateObject())
            {
                if (!KnownKeys.Contains(property.Name))
                {
                    unknown[property.Name] = property.Value.Clone();
                }
            }

            var sidecar = new AnnotationsSidecar { Schema = schema, Annotations = list, UnknownFields = unknown };
            Validate(sidecar, source);
            return sidecar;
        }
    }

    /// <summary>Checks what JSON syntax cannot express. <paramref name="source"/> prefixes error messages.</summary>
    /// <exception cref="SidecarException"/>
    public static void Validate(AnnotationsSidecar sidecar, string source)
    {
        ArgumentNullException.ThrowIfNull(sidecar);
        ArgumentNullException.ThrowIfNull(source);

        void Fail(string key, string problem) => throw new SidecarException($"{source}: {key}: {problem}", source);

        if (sidecar.Schema < 1)
        {
            Fail("schema", "must be 1 or greater");
        }

        var ids = new HashSet<Ulid>();
        for (var i = 0; i < sidecar.Annotations.Count; i++)
        {
            var a = sidecar.Annotations[i];
            var key = $"annotations[{i + 1}]";
            if (!ids.Add(a.Id))
            {
                Fail($"{key}.id", $"duplicate ID {a.Id}");
            }

            if (a.File.Length == 0 || a.File is "." or ".." || a.File.IndexOfAny(['/', '\\', '\0']) >= 0)
            {
                Fail($"{key}.file", "must be a file name without a folder");
            }

            switch (a.Kind)
            {
                case AnnotationKind.Highlight:
                    if (string.IsNullOrWhiteSpace(a.Locator) || a.Locator.Length > MaxLocatorLength)
                    {
                        Fail($"{key}.locator", $"a highlight needs a locator of at most {MaxLocatorLength} characters");
                    }

                    if (a.Quote is null || string.IsNullOrWhiteSpace(a.Quote.Exact))
                    {
                        Fail($"{key}.quote.exact", "a highlight needs the marked text");
                    }

                    if (a.Color is null || !AnnotationsSidecar.Colors.Contains(a.Color))
                    {
                        Fail($"{key}.color", $"must be one of {string.Join(", ", AnnotationsSidecar.Colors)}");
                    }

                    break;
                case AnnotationKind.Note:
                    if (a.Locator is not null || a.Quote is not null || a.Color is not null)
                    {
                        Fail($"{key}.kind", "a note has no locator, quote or color");
                    }

                    if (string.IsNullOrWhiteSpace(a.Note))
                    {
                        Fail($"{key}.note", "a note needs a text");
                    }

                    break;
            }

            if (a.Rects is { } rects)
            {
                if (a.Kind != AnnotationKind.Highlight || rects.Count is < 1 or > MaxRects)
                {
                    Fail($"{key}.rects", $"only a highlight has rects, 1 to {MaxRects}");
                }

                for (var r = 0; r < rects.Count; r++)
                {
                    var rect = rects[r];
                    if (!IsFraction(rect.X) || !IsFraction(rect.Y) || !IsFraction(rect.Width) || !IsFraction(rect.Height)
                        || rect.X + rect.Width > 1.0001 || rect.Y + rect.Height > 1.0001)
                    {
                        Fail($"{key}.rects[{r + 1}]", "must be x, y, width, height as fractions of the page, inside it");
                    }
                }
            }

            if (a.Note is { } note && note.Length == 0)
            {
                Fail($"{key}.note", "must not be empty (leave it out)");
            }
        }
    }

    public static string Serialize(AnnotationsSidecar sidecar)
    {
        ArgumentNullException.ThrowIfNull(sidecar);

        using var buffer = new MemoryStream();
        using (var w = new Utf8JsonWriter(buffer, WriterOptions))
        {
            w.WriteStartObject();
            w.WriteNumber("schema", sidecar.Schema);
            w.WriteStartArray("annotations");
            foreach (var a in sidecar.Annotations)
            {
                w.WriteStartObject();
                w.WriteString("id", a.Id.ToString());
                w.WriteString("file", a.File);
                w.WriteString("kind", KindName(a.Kind));
                Optional(w, "locator", a.Locator);
                if (a.Quote is { } quote)
                {
                    w.WriteStartObject("quote");
                    w.WriteString("exact", quote.Exact);
                    Optional(w, "prefix", quote.Prefix);
                    Optional(w, "suffix", quote.Suffix);
                    w.WriteEndObject();
                }

                Optional(w, "color", a.Color);
                Optional(w, "note", a.Note);
                if (a.Rects is { } rects)
                {
                    w.WriteStartArray("rects");
                    foreach (var rect in rects)
                    {
                        w.WriteStartArray();
                        w.WriteNumberValue(rect.X);
                        w.WriteNumberValue(rect.Y);
                        w.WriteNumberValue(rect.Width);
                        w.WriteNumberValue(rect.Height);
                        w.WriteEndArray();
                    }

                    w.WriteEndArray();
                }

                w.WriteString("createdAt", FormatTimestamp(a.CreatedAt));
                w.WriteString("updatedAt", FormatTimestamp(a.UpdatedAt));
                w.WriteEndObject();
            }

            w.WriteEndArray();
            foreach (var (name, value) in sidecar.UnknownFields)
            {
                w.WritePropertyName(name);
                value.WriteTo(w);
            }

            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length) + "\n";
    }

    public static string KindName(AnnotationKind kind) => kind == AnnotationKind.Highlight ? "highlight" : "note";

    public static bool TryParseKind(string? name, out AnnotationKind kind)
    {
        kind = default;
        switch (name)
        {
            case "highlight":
                kind = AnnotationKind.Highlight;
                return true;
            case "note":
                kind = AnnotationKind.Note;
                return true;
            default:
                return false;
        }
    }

    private static Annotation ReadAnnotation(JsonElement item, string key, string source)
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            throw new SidecarException($"{source}: {key}: must be an object", source);
        }

        string? Text(JsonElement parent, string name, string path, bool required = false)
        {
            if (!parent.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
            {
                return required ? throw new SidecarException($"{source}: {path}: missing", source) : null;
            }

            return value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : throw new SidecarException($"{source}: {path}: must be a string", source);
        }

        DateTimeOffset Time(string name)
        {
            var text = Text(item, name, $"{key}.{name}", required: true)!;
            return DateTimeOffset.TryParseExact(text, TimestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var time)
                ? time
                : throw new SidecarException($"{source}: {key}.{name}: not a UTC time like 2026-10-25T10:00:00Z", source);
        }

        if (!Ulid.TryParse(Text(item, "id", $"{key}.id", required: true), out var id))
        {
            throw new SidecarException($"{source}: {key}.id: not a valid ULID", source);
        }

        if (!TryParseKind(Text(item, "kind", $"{key}.kind", required: true), out var kind))
        {
            throw new SidecarException($"{source}: {key}.kind: must be highlight or note", source);
        }

        AnnotationQuote? quote = null;
        if (item.TryGetProperty("quote", out var q) && q.ValueKind != JsonValueKind.Null)
        {
            if (q.ValueKind != JsonValueKind.Object)
            {
                throw new SidecarException($"{source}: {key}.quote: must be an object", source);
            }

            quote = new AnnotationQuote(
                Text(q, "exact", $"{key}.quote.exact", required: true)!,
                Text(q, "prefix", $"{key}.quote.prefix"),
                Text(q, "suffix", $"{key}.quote.suffix"));
        }

        List<AnnotationRect>? rects = null;
        if (item.TryGetProperty("rects", out var r) && r.ValueKind != JsonValueKind.Null)
        {
            if (r.ValueKind != JsonValueKind.Array)
            {
                throw new SidecarException($"{source}: {key}.rects: must be an array", source);
            }

            rects = [];
            foreach (var entry in r.EnumerateArray())
            {
                var numbers = entry.ValueKind == JsonValueKind.Array ? entry.EnumerateArray().ToList() : [];
                if (numbers.Count != 4 || numbers.Any(n => n.ValueKind != JsonValueKind.Number))
                {
                    throw new SidecarException($"{source}: {key}.rects[{rects.Count + 1}]: must be four numbers", source);
                }

                rects.Add(new AnnotationRect(numbers[0].GetDouble(), numbers[1].GetDouble(), numbers[2].GetDouble(), numbers[3].GetDouble()));
            }
        }

        return new Annotation(
            id,
            Text(item, "file", $"{key}.file", required: true)!,
            kind,
            Text(item, "locator", $"{key}.locator"),
            quote,
            Text(item, "color", $"{key}.color"),
            Text(item, "note", $"{key}.note"),
            Time("createdAt"),
            Time("updatedAt"),
            rects);
    }

    private static bool IsFraction(double value) => value is >= 0 and <= 1;

    private static string FormatTimestamp(DateTimeOffset time) =>
        time.UtcDateTime.ToString(TimestampFormat, CultureInfo.InvariantCulture);

    private static void Optional(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is not null)
        {
            writer.WriteString(name, value);
        }
    }
}
