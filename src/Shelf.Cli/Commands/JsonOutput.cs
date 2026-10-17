using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Shelf.Cli.Commands;

/// <summary>Small JSON objects for <c>--json</c> output, written without reflection (Native AOT).</summary>
internal static class JsonOutput
{
    /// <summary>A single-line object. Values are strings or integers.</summary>
    public static string Object(params (string Name, object Value)[] properties)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            writer.WriteStartObject();
            foreach (var (name, value) in properties)
            {
                switch (value)
                {
                    case string text:
                        writer.WriteString(name, text);
                        break;
                    case int number:
                        writer.WriteNumber(name, number);
                        break;
                    default:
                        throw new ArgumentException($"Unsupported JSON value for '{name}'.", nameof(properties));
                }
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }
}
