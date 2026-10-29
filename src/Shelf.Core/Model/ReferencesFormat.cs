using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Shelf.Core.Model;

/// <summary>
/// Reads, validates and writes the JSON of a <c>references.json</c> (ADR 0019). Written by hand like <see cref="AnnotationsFormat"/>:
/// error messages name the key, the key order is fixed, no reflection (Native AOT).
/// </summary>
public static class ReferencesFormat
{
    public const string FileName = "references.json";

    /// <summary>The most entries a list may have (the longest reference lists of papers are some hundreds).</summary>
    public const int MaxReferences = 5000;

    private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    private static readonly HashSet<string> KnownKeys = ["schema", "source", "fetchedAt", "references"];

    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <exception cref="SidecarException"/>
    public static ReferencesSidecar Parse(string text, string source)
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

            string? Text(JsonElement parent, string name, string path)
            {
                if (!parent.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
                {
                    return null;
                }

                return value.ValueKind == JsonValueKind.String
                    ? value.GetString()
                    : throw new SidecarException($"{source}: {path}: must be a string", source);
            }

            var schema = 1;
            if (root.TryGetProperty("schema", out var schemaElement)
                && (schemaElement.ValueKind != JsonValueKind.Number || !schemaElement.TryGetInt32(out schema)))
            {
                throw new SidecarException($"{source}: schema: must be a whole number", source);
            }

            if (schema > ReferencesSidecar.CurrentSchema)
            {
                throw new SidecarException(
                    $"{source}: schema {schema} is newer than this version of shelf supports ({ReferencesSidecar.CurrentSchema})",
                    source);
            }

            DateTimeOffset? fetched = null;
            if (Text(root, "fetchedAt", "fetchedAt") is { } stamp)
            {
                fetched = DateTimeOffset.TryParseExact(stamp, TimestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var time)
                    ? time
                    : throw new SidecarException($"{source}: fetchedAt: not a UTC time like 2026-10-25T10:00:00Z", source);
            }

            var list = new List<Reference>();
            if (root.TryGetProperty("references", out var array))
            {
                if (array.ValueKind != JsonValueKind.Array)
                {
                    throw new SidecarException($"{source}: references: must be an array", source);
                }

                var i = 0;
                foreach (var item in array.EnumerateArray())
                {
                    var key = $"references[{++i}]";
                    if (item.ValueKind != JsonValueKind.Object)
                    {
                        throw new SidecarException($"{source}: {key}: must be an object", source);
                    }

                    int? year = null;
                    if (item.TryGetProperty("year", out var y) && y.ValueKind != JsonValueKind.Null)
                    {
                        year = y.ValueKind == JsonValueKind.Number && y.TryGetInt32(out var n)
                            ? n
                            : throw new SidecarException($"{source}: {key}.year: must be a whole number", source);
                    }

                    list.Add(new Reference(
                        Text(item, "doi", $"{key}.doi"),
                        Text(item, "title", $"{key}.title"),
                        year,
                        Text(item, "author", $"{key}.author"),
                        Text(item, "unstructured", $"{key}.unstructured")));
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

            var sidecar = new ReferencesSidecar
            {
                Schema = schema,
                Source = Text(root, "source", "source"),
                FetchedAt = fetched,
                References = list,
                UnknownFields = unknown,
            };
            Validate(sidecar, source);
            return sidecar;
        }
    }

    /// <summary>Checks what JSON syntax cannot express. <paramref name="source"/> prefixes error messages.</summary>
    /// <exception cref="SidecarException"/>
    public static void Validate(ReferencesSidecar sidecar, string source)
    {
        ArgumentNullException.ThrowIfNull(sidecar);
        ArgumentNullException.ThrowIfNull(source);

        void Fail(string key, string problem) => throw new SidecarException($"{source}: {key}: {problem}", source);

        if (sidecar.Schema < 1)
        {
            Fail("schema", "must be 1 or greater");
        }

        if (sidecar.References.Count > MaxReferences)
        {
            Fail("references", $"at most {MaxReferences} entries");
        }

        for (var i = 0; i < sidecar.References.Count; i++)
        {
            var r = sidecar.References[i];
            var key = $"references[{i + 1}]";
            if (string.IsNullOrWhiteSpace(r.Doi) && string.IsNullOrWhiteSpace(r.Title) && string.IsNullOrWhiteSpace(r.Unstructured))
            {
                Fail(key, "needs a doi, a title or the text of the citation");
            }

            if (r.Doi is { } doi && (doi.Length == 0 || doi != doi.Trim()))
            {
                Fail($"{key}.doi", "must not be empty or have spaces around it (leave it out)");
            }
        }
    }

    public static string Serialize(ReferencesSidecar sidecar)
    {
        ArgumentNullException.ThrowIfNull(sidecar);

        using var buffer = new MemoryStream();
        using (var w = new Utf8JsonWriter(buffer, WriterOptions))
        {
            w.WriteStartObject();
            w.WriteNumber("schema", sidecar.Schema);
            Optional(w, "source", sidecar.Source);
            if (sidecar.FetchedAt is { } fetched)
            {
                w.WriteString("fetchedAt", fetched.UtcDateTime.ToString(TimestampFormat, CultureInfo.InvariantCulture));
            }

            w.WriteStartArray("references");
            foreach (var r in sidecar.References)
            {
                w.WriteStartObject();
                Optional(w, "doi", r.Doi);
                Optional(w, "title", r.Title);
                if (r.Year is { } year)
                {
                    w.WriteNumber("year", year);
                }

                Optional(w, "author", r.Author);
                Optional(w, "unstructured", r.Unstructured);
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

    private static void Optional(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is not null)
        {
            writer.WriteString(name, value);
        }
    }
}
