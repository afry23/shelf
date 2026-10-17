using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Shelf.Cli.Commands;

/// <summary>JSON for <c>--json</c> output, written without reflection (Native AOT).</summary>
internal static class JsonOutput
{
    private static readonly JsonWriterOptions Options = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>A single-line object. Values are strings or integers.</summary>
    public static string Object(params (string Name, object Value)[] properties) =>
        Write(writer =>
        {
            writer.WriteStartObject();
            foreach (var (name, value) in properties)
            {
                Property(writer, name, value);
            }

            writer.WriteEndObject();
        });

    /// <summary>A single-line JSON document written by <paramref name="write"/>.</summary>
    public static string Write(Action<Utf8JsonWriter> write)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, Options))
        {
            write(writer);
        }

        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    /// <summary>One property; null values are left out. Strings, integers, doubles and booleans are supported.</summary>
    public static void Property(Utf8JsonWriter writer, string name, object? value)
    {
        switch (value)
        {
            case null:
                break;
            case string text:
                writer.WriteString(name, text);
                break;
            case int number:
                writer.WriteNumber(name, number);
                break;
            case double number:
                writer.WriteNumber(name, number);
                break;
            case bool flag:
                writer.WriteBoolean(name, flag);
                break;
            default:
                throw new ArgumentException($"Unsupported JSON value for '{name}'.", nameof(value));
        }
    }
}
