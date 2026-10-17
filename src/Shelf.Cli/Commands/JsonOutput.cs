using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Shelf.Cli.Commands;

/// <summary>Small JSON objects for <c>--json</c> output, written without reflection (Native AOT).</summary>
internal static class JsonOutput
{
    /// <summary>A single-line object whose values are all strings.</summary>
    public static string Object(params (string Name, string Value)[] properties)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            writer.WriteStartObject();
            foreach (var (name, value) in properties)
            {
                writer.WriteString(name, value);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }
}
