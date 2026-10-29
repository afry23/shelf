using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Shelf.Core.Model;

/// <summary>
/// Reads, validates and writes the JSON of a <c>book.json</c> (PRD 10.4). Written by hand on
/// <see cref="JsonDocument"/> and <see cref="Utf8JsonWriter"/> so that error messages name the exact key
/// (<c>files[2].name</c>), the key order is fixed and no reflection is involved (Native AOT).
/// </summary>
public static class SidecarFormat
{
    private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

    private static readonly HashSet<string> KnownKeys =
    [
        "schema", "id", "title", "subtitle", "authors", "language", "year", "isbn10", "isbn13", "publisher",
        "series", "seriesIndex", "edition", "citeKey", "description", "tags", "rating", "readStatus", "finishedAt", "added", "cover",
        "files", "notDuplicates", "related",
        "kind", "doi", "arxivId", "pmid", "venue", "volume", "issue", "pages", "abstract", "keywords",
        "sourceUrl", "site", "published", "captured",
    ];

    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
        NewLine = "\n",
        // Keeps non-ASCII text readable in the file; the file is UTF-8 and never embedded in HTML.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Parses and validates the text of a sidecar. <paramref name="source"/> prefixes error messages.</summary>
    /// <exception cref="SidecarException"/>
    public static WorkSidecar Parse(string text, string source)
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
                throw new SidecarException($"{source}: not valid JSON: expected an object, found {Kind(root)}", source);
            }

            var reader = new Reader(source);
            var schema = reader.Integer(root, "schema", "") ?? 0;
            if (schema > WorkSidecar.CurrentSchema)
            {
                throw new SidecarException(
                    $"{source}: schema {schema} is newer than this version of shelf supports ({WorkSidecar.CurrentSchema})",
                    source);
            }

            var sidecar = reader.ReadSidecar(root, (int)schema);
            Validate(sidecar, source);
            return sidecar;
        }
    }

    /// <summary>Checks the rules of PRD 6.1 and 10.4 that JSON syntax cannot express.</summary>
    /// <exception cref="SidecarException"/>
    public static void Validate(WorkSidecar sidecar, string source)
    {
        ArgumentNullException.ThrowIfNull(sidecar);
        ArgumentNullException.ThrowIfNull(source);

        void Fail(string key, string problem) => throw new SidecarException($"{source}: {key}: {problem}", source);

        if (sidecar.Schema < 1)
        {
            Fail("schema", "must be 1 or greater");
        }

        if (sidecar.Schema > WorkSidecar.CurrentSchema)
        {
            throw new SidecarException(
                $"{source}: schema {sidecar.Schema} is newer than this version of shelf supports ({WorkSidecar.CurrentSchema})",
                source);
        }

        if (string.IsNullOrWhiteSpace(sidecar.Title))
        {
            Fail("title", "must not be empty");
        }

        if (sidecar.Authors.Count == 0 || sidecar.Authors.Any(string.IsNullOrWhiteSpace))
        {
            Fail("authors", "at least one author, none of them empty");
        }

        if (sidecar.Rating is < 1 or > 5)
        {
            Fail("rating", "must be between 1 and 5");
        }

        if (sidecar.Isbn13 is { } isbn13 && !(isbn13.Length == 13 && isbn13.All(char.IsAsciiDigit)))
        {
            Fail("isbn13", "must be 13 digits");
        }

        if (sidecar.Isbn10 is { } isbn10
            && !(isbn10.Length == 10 && isbn10[..9].All(char.IsAsciiDigit) && (char.IsAsciiDigit(isbn10[9]) || isbn10[9] == 'X')))
        {
            Fail("isbn10", "must be 9 digits and a digit or X");
        }

        for (var i = 0; i < sidecar.Tags.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(sidecar.Tags[i]))
            {
                Fail($"tags[{i + 1}]", "must not be empty");
            }
        }

        if (sidecar.Files.Count == 0)
        {
            Fail("files", "at least one file");
        }

        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < sidecar.Files.Count; i++)
        {
            var file = sidecar.Files[i];
            var key = $"files[{i + 1}]";
            if (!IsFileName(file.Name))
            {
                Fail($"{key}.name", "must be a file name without a folder");
            }

            if (!names.Add(file.Name))
            {
                Fail($"{key}.name", $"duplicate file name '{file.Name}'");
            }

            if (file.Progress is { } progress && string.IsNullOrWhiteSpace(progress.Locator))
            {
                Fail($"{key}.progress.locator", "must not be empty");
            }
        }

        for (var i = 0; i < sidecar.Related.Count; i++)
        {
            if (sidecar.Related[i] == sidecar.Id)
            {
                Fail($"related[{i + 1}]", "must not be the work's own ID");
            }
        }

        for (var i = 0; i < sidecar.NotDuplicates.Files.Count; i++)
        {
            var pair = sidecar.NotDuplicates.Files[i];
            if (!IsFileName(pair.A) || !IsFileName(pair.B))
            {
                Fail($"notDuplicates.files[{i + 1}]", "must be a pair of file names");
            }
        }
    }

    /// <summary>The text of the sidecar: indented, LF line endings, final newline, fixed key order.</summary>
    public static string Serialize(WorkSidecar sidecar)
    {
        ArgumentNullException.ThrowIfNull(sidecar);

        using var buffer = new MemoryStream();
        using (var w = new Utf8JsonWriter(buffer, WriterOptions))
        {
            w.WriteStartObject();
            w.WriteNumber("schema", sidecar.Schema);
            w.WriteString("id", sidecar.Id.ToString());
            w.WriteString("title", sidecar.Title);
            Optional(w, "subtitle", sidecar.Subtitle);
            w.WriteStartArray("authors");
            foreach (var author in sidecar.Authors)
            {
                w.WriteStringValue(author);
            }

            w.WriteEndArray();
            Optional(w, "language", sidecar.Language);
            if (sidecar.Year is { } year)
            {
                w.WriteNumber("year", year);
            }

            Optional(w, "isbn10", sidecar.Isbn10);
            Optional(w, "isbn13", sidecar.Isbn13);
            Optional(w, "publisher", sidecar.Publisher);
            Optional(w, "series", sidecar.Series);
            if (sidecar.SeriesIndex is { } seriesIndex)
            {
                w.WriteNumber("seriesIndex", seriesIndex);
            }

            Optional(w, "edition", sidecar.Edition);
            Optional(w, "citeKey", sidecar.CiteKey);
            Optional(w, "description", sidecar.Description);
            if (sidecar.Kind != WorkKind.Book)
            {
                w.WriteString("kind", sidecar.Kind == WorkKind.Paper ? "paper" : "article");
            }

            Optional(w, "doi", sidecar.Doi);
            Optional(w, "arxivId", sidecar.ArxivId);
            Optional(w, "pmid", sidecar.Pmid);
            Optional(w, "venue", sidecar.Venue);
            Optional(w, "sourceUrl", sidecar.SourceUrl);
            Optional(w, "site", sidecar.Site);
            if (sidecar.Published is { } published)
            {
                w.WriteString("published", FormatTimestamp(published));
            }

            if (sidecar.Captured is { } captured)
            {
                w.WriteString("captured", FormatTimestamp(captured));
            }

            Optional(w, "volume", sidecar.Volume);
            Optional(w, "issue", sidecar.Issue);
            Optional(w, "pages", sidecar.Pages);
            Optional(w, "abstract", sidecar.Abstract);
            if (sidecar.Keywords.Count > 0)
            {
                w.WriteStartArray("keywords");
                foreach (var keyword in sidecar.Keywords)
                {
                    w.WriteStringValue(keyword);
                }

                w.WriteEndArray();
            }

            w.WriteStartArray("tags");
            foreach (var tag in sidecar.Tags)
            {
                w.WriteStringValue(tag);
            }

            w.WriteEndArray();
            if (sidecar.Rating is { } rating)
            {
                w.WriteNumber("rating", rating);
            }

            w.WriteString("readStatus", ReadStatusName(sidecar.ReadStatus));
            if (sidecar.FinishedAt is { } finished)
            {
                w.WriteString("finishedAt", FormatTimestamp(finished));
            }

            if (sidecar.Added is { } added)
            {
                w.WriteString("added", FormatTimestamp(added));
            }

            Optional(w, "cover", sidecar.Cover);
            w.WriteStartArray("files");
            foreach (var file in sidecar.Files)
            {
                w.WriteStartObject();
                w.WriteString("name", file.Name);
                if (file.Progress is { } progress)
                {
                    w.WriteStartObject("progress");
                    w.WriteString("locator", progress.Locator);
                    w.WriteString("updatedAt", FormatTimestamp(progress.UpdatedAt));
                    w.WriteEndObject();
                }

                w.WriteEndObject();
            }

            w.WriteEndArray();
            if (!sidecar.NotDuplicates.IsEmpty)
            {
                w.WriteStartObject("notDuplicates");
                w.WriteStartArray("works");
                foreach (var work in sidecar.NotDuplicates.Works)
                {
                    w.WriteStringValue(work.ToString());
                }

                w.WriteEndArray();
                w.WriteStartArray("files");
                foreach (var pair in sidecar.NotDuplicates.Files)
                {
                    w.WriteStartArray();
                    w.WriteStringValue(pair.A);
                    w.WriteStringValue(pair.B);
                    w.WriteEndArray();
                }

                w.WriteEndArray();
                w.WriteEndObject();
            }

            if (sidecar.Related.Count > 0)
            {
                w.WriteStartArray("related");
                foreach (var work in sidecar.Related)
                {
                    w.WriteStringValue(work.ToString());
                }

                w.WriteEndArray();
            }

            foreach (var (name, value) in sidecar.UnknownFields)
            {
                w.WritePropertyName(name);
                value.WriteTo(w);
            }

            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length) + "\n";
    }

    internal static string ReadStatusName(ReadStatus status) => status switch
    {
        ReadStatus.Unread => "unread",
        ReadStatus.Reading => "reading",
        _ => "read",
    };

    private static string FormatTimestamp(DateTimeOffset time) =>
        time.UtcDateTime.ToString(TimestampFormat, CultureInfo.InvariantCulture);

    private static void Optional(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is not null)
        {
            writer.WriteString(name, value);
        }
    }

    private static bool IsFileName(string name) =>
        name.Length > 0 && name is not ("." or "..") && name.IndexOfAny(['/', '\\', '\0']) < 0;

    private static string Kind(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => "string",
        JsonValueKind.Number => "number",
        JsonValueKind.True or JsonValueKind.False => "boolean",
        JsonValueKind.Array => "array",
        JsonValueKind.Object => "object",
        _ => "null",
    };

    /// <summary>Typed access to JSON properties with errors that name the full key.</summary>
    private sealed class Reader(string source)
    {
        public WorkSidecar ReadSidecar(JsonElement root, int schema)
        {
            var idText = String(root, "id", "");
            if (!Ulid.TryParse(idText, out var id))
            {
                throw Fail("id", "not a valid ULID");
            }

            var unknown = new Dictionary<string, JsonElement>();
            foreach (var property in root.EnumerateObject())
            {
                if (!KnownKeys.Contains(property.Name))
                {
                    unknown[property.Name] = property.Value.Clone();
                }
            }

            return new WorkSidecar
            {
                Schema = schema,
                Id = id,
                Title = String(root, "title", "") ?? string.Empty,
                Subtitle = String(root, "subtitle", ""),
                Authors = Strings(root, "authors", ""),
                Language = String(root, "language", ""),
                Year = Int32(root, "year", ""),
                Isbn10 = String(root, "isbn10", ""),
                Isbn13 = String(root, "isbn13", ""),
                Publisher = String(root, "publisher", ""),
                Series = String(root, "series", ""),
                SeriesIndex = Number(root, "seriesIndex", ""),
                Edition = String(root, "edition", ""),
                CiteKey = String(root, "citeKey", ""),
                Description = String(root, "description", ""),
                Kind = ReadKind(root),
                Doi = String(root, "doi", ""),
                ArxivId = String(root, "arxivId", ""),
                Pmid = String(root, "pmid", ""),
                Venue = String(root, "venue", ""),
                SourceUrl = String(root, "sourceUrl", ""),
                Site = String(root, "site", ""),
                Published = Timestamp(root, "published", ""),
                Captured = Timestamp(root, "captured", ""),
                Volume = String(root, "volume", ""),
                Issue = String(root, "issue", ""),
                Pages = String(root, "pages", ""),
                Abstract = String(root, "abstract", ""),
                Keywords = Strings(root, "keywords", ""),
                Tags = Strings(root, "tags", ""),
                Rating = Int32(root, "rating", ""),
                ReadStatus = ReadStatus(root),
                FinishedAt = Timestamp(root, "finishedAt", ""),
                Added = Timestamp(root, "added", ""),
                Cover = String(root, "cover", ""),
                Files = Files(root),
                NotDuplicates = NotDuplicates(root),
                Related = Related(root),
                UnknownFields = unknown,
            };
        }

        public long? Integer(JsonElement obj, string name, string prefix)
        {
            if (!Find(obj, name, out var value))
            {
                return null;
            }

            return value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number)
                ? number
                : throw Fail(prefix + name, $"expected integer, found {Kind(value)}");
        }

        private WorkKind ReadKind(JsonElement root)
        {
            return String(root, "kind", "") switch
            {
                null or "book" => WorkKind.Book,
                "paper" => WorkKind.Paper,
                "article" => WorkKind.Article,
                _ => throw Fail("kind", "must be one of book, paper, article"),
            };
        }

        private ReadStatus ReadStatus(JsonElement root)
        {
            return String(root, "readStatus", "") switch
            {
                null or "unread" => Model.ReadStatus.Unread,
                "reading" => Model.ReadStatus.Reading,
                "read" => Model.ReadStatus.Read,
                _ => throw Fail("readStatus", "must be one of unread, reading, read"),
            };
        }

        private List<SidecarFile> Files(JsonElement root)
        {
            var files = new List<SidecarFile>();
            foreach (var (item, index) in Items(root, "files", ""))
            {
                var prefix = $"files[{index}].";
                if (item.ValueKind != JsonValueKind.Object)
                {
                    throw Fail($"files[{index}]", $"expected object, found {Kind(item)}");
                }

                FileProgress? progress = null;
                if (Find(item, "progress", out var progressElement))
                {
                    if (progressElement.ValueKind != JsonValueKind.Object)
                    {
                        throw Fail(prefix + "progress", $"expected object, found {Kind(progressElement)}");
                    }

                    var progressPrefix = prefix + "progress.";
                    var updatedAt = Timestamp(progressElement, "updatedAt", progressPrefix)
                        ?? throw Fail(progressPrefix + "updatedAt", "must be a UTC time like 2026-10-12T19:04:00Z");
                    progress = new FileProgress(String(progressElement, "locator", progressPrefix) ?? string.Empty, updatedAt);
                }

                files.Add(new SidecarFile(String(item, "name", prefix) ?? string.Empty, progress));
            }

            return files;
        }

        private List<Ulid> Related(JsonElement root)
        {
            var related = new List<Ulid>();
            foreach (var (item, index) in Items(root, "related", ""))
            {
                if (item.ValueKind != JsonValueKind.String || !Ulid.TryParse(item.GetString(), out var work))
                {
                    throw Fail($"related[{index}]", "not a valid ULID");
                }

                related.Add(work);
            }

            return related;
        }

        private NotDuplicates NotDuplicates(JsonElement root)
        {
            if (!Find(root, "notDuplicates", out var element))
            {
                return Model.NotDuplicates.None;
            }

            if (element.ValueKind != JsonValueKind.Object)
            {
                throw Fail("notDuplicates", $"expected object, found {Kind(element)}");
            }

            var works = new List<Ulid>();
            foreach (var (item, index) in Items(element, "works", "notDuplicates."))
            {
                if (item.ValueKind != JsonValueKind.String || !Ulid.TryParse(item.GetString(), out var work))
                {
                    throw Fail($"notDuplicates.works[{index}]", "not a valid ULID");
                }

                works.Add(work);
            }

            var pairs = new List<FilePair>();
            foreach (var (item, index) in Items(element, "files", "notDuplicates."))
            {
                var names = item.ValueKind == JsonValueKind.Array
                    ? item.EnumerateArray().Select(e => e.ValueKind == JsonValueKind.String ? e.GetString()! : null).ToList()
                    : [];
                if (names.Count != 2 || names.Any(n => n is null))
                {
                    throw Fail($"notDuplicates.files[{index}]", "must be a pair of file names");
                }

                pairs.Add(new FilePair(names[0]!, names[1]!));
            }

            return new Model.NotDuplicates(works, pairs);
        }

        private string? String(JsonElement obj, string name, string prefix)
        {
            if (!Find(obj, name, out var value))
            {
                return null;
            }

            return value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : throw Fail(prefix + name, $"expected string, found {Kind(value)}");
        }

        private int? Int32(JsonElement obj, string name, string prefix)
        {
            var value = Integer(obj, name, prefix);
            return value is null ? null
                : value is >= int.MinValue and <= int.MaxValue ? (int)value
                : throw Fail(prefix + name, "expected integer, found a number out of range");
        }

        private double? Number(JsonElement obj, string name, string prefix)
        {
            if (!Find(obj, name, out var value))
            {
                return null;
            }

            return value.ValueKind == JsonValueKind.Number
                ? value.GetDouble()
                : throw Fail(prefix + name, $"expected number, found {Kind(value)}");
        }

        private DateTimeOffset? Timestamp(JsonElement obj, string name, string prefix)
        {
            var text = String(obj, name, prefix);
            if (text is null)
            {
                return null;
            }

            return DateTimeOffset.TryParseExact(
                text,
                TimestampFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var time)
                ? time
                : throw Fail(prefix + name, "must be a UTC time like 2026-10-12T19:04:00Z");
        }

        private List<string> Strings(JsonElement obj, string name, string prefix)
        {
            var list = new List<string>();
            foreach (var (item, index) in Items(obj, name, prefix))
            {
                list.Add(item.ValueKind == JsonValueKind.String
                    ? item.GetString()!
                    : throw Fail($"{prefix}{name}[{index}]", $"expected string, found {Kind(item)}"));
            }

            return list;
        }

        /// <summary>The 1-based elements of an array property; none if absent.</summary>
        private IEnumerable<(JsonElement Item, int Index)> Items(JsonElement obj, string name, string prefix)
        {
            if (!Find(obj, name, out var value))
            {
                return [];
            }

            if (value.ValueKind != JsonValueKind.Array)
            {
                throw Fail(prefix + name, $"expected array, found {Kind(value)}");
            }

            return value.EnumerateArray().Select((item, i) => (item, i + 1));
        }

        /// <summary>A property that exists and is not JSON <c>null</c>.</summary>
        private static bool Find(JsonElement obj, string name, out JsonElement value) =>
            obj.TryGetProperty(name, out value) && value.ValueKind != JsonValueKind.Null;

        private SidecarException Fail(string key, string problem) => new($"{source}: {key}: {problem}", source);
    }
}
