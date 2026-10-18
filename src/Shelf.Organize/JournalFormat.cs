using System.Globalization;
using System.Text.Json;

namespace Shelf.Organize;

/// <summary>Reads and writes journal files without reflection (Native AOT).</summary>
internal static class JournalFormat
{
    public const int Schema = 1;

    public static byte[] Serialize(JournalData data)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            w.WriteStartObject();
            w.WriteNumber("schema", Schema);
            w.WriteString("label", data.Label);
            w.WriteBoolean("undoable", data.Undoable);
            w.WriteString("started", data.Started.ToString("O", CultureInfo.InvariantCulture));
            w.WriteString("state", data.State.ToString().ToLowerInvariant());
            w.WriteStartArray("ops");
            foreach (var op in data.Ops)
            {
                w.WriteStartObject();
                w.WriteString("kind", op.Kind.ToString().ToLowerInvariant());
                w.WriteString("from", op.From);
                w.WriteString("to", op.To);
                if (op.Sha256 is not null)
                {
                    w.WriteString("sha256", op.Sha256);
                }

                w.WriteString("status", op.Status.ToString().ToLowerInvariant());
                w.WriteEndObject();
            }

            w.WriteEndArray();
            w.WriteEndObject();
        }

        return stream.ToArray();
    }

    /// <exception cref="JournalException"/>
    public static JournalData Parse(string path)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
            var root = document.RootElement;
            if (root.GetProperty("schema").GetInt32() != Schema)
            {
                throw new JournalException($"{path}: unsupported journal schema");
            }

            var data = new JournalData(
                root.GetProperty("label").GetString()!,
                root.GetProperty("undoable").GetBoolean(),
                DateTimeOffset.Parse(root.GetProperty("started").GetString()!, CultureInfo.InvariantCulture))
            {
                State = Enum.Parse<JournalState>(root.GetProperty("state").GetString()!, ignoreCase: true),
            };
            foreach (var op in root.GetProperty("ops").EnumerateArray())
            {
                data.Ops.Add(new JournalOp(
                    Enum.Parse<JournalOpKind>(op.GetProperty("kind").GetString()!, ignoreCase: true),
                    op.GetProperty("from").GetString()!,
                    op.GetProperty("to").GetString()!,
                    op.TryGetProperty("sha256", out var sha) ? sha.GetString() : null,
                    Enum.Parse<JournalOpStatus>(op.GetProperty("status").GetString()!, ignoreCase: true)));
            }

            return data;
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or ArgumentException or IOException)
        {
            throw new JournalException($"{path}: damaged journal ({e.Message})", e);
        }
    }
}
