using System.Globalization;
using Proto;
using Tomlet;
using Tomlet.Exceptions;
using Tomlet.Models;

return Harness.Run(new TomletAdapter(), args, Console.Out, Console.Error);

internal sealed class TomletAdapter : IAdapter
{
    public object Parse(string text)
    {
        try
        {
            return new TomlParser().Parse(text);
        }
        catch (TomlExceptionWithLine e)
        {
            // LineNumber is protected; the public surface only exposes the line inside the message.
            throw new ParseFailure(LineFromMessage(e.Message), null, e.Message);
        }
        catch (TomlException e)
        {
            throw new ParseFailure(null, null, e.Message);
        }
    }

    // No regex on purpose: it would distort the binary size measurement.
    private static int? LineFromMessage(string message)
    {
        const string marker = "line ";
        var at = message.IndexOf(marker, StringComparison.Ordinal);
        if (at < 0)
        {
            return null;
        }

        var end = at + marker.Length;
        while (end < message.Length && char.IsAsciiDigit(message[end]))
        {
            end++;
        }

        return int.TryParse(message.AsSpan(at + marker.Length, end - at - marker.Length), CultureInfo.InvariantCulture, out var n) ? n : null;
    }

    public Val? Get(object document, string dottedKey)
    {
        TomlValue? current = (TomlTable)document;
        foreach (var part in dottedKey.Split('.'))
        {
            if (current is not TomlTable table || !table.Entries.TryGetValue(part, out current))
            {
                return null;
            }
        }

        return current switch
        {
            TomlString s => new Val("string", s.Value),
            TomlLong l => new Val("integer", l.Value.ToString(CultureInfo.InvariantCulture)),
            TomlDouble d => new Val("float", Harness.Float(d.Value)),
            TomlBoolean b => new Val("boolean", b.Value ? "true" : "false"),
            TomlOffsetDateTime or TomlLocalDateTime or TomlLocalDate or TomlLocalTime => new Val("datetime", current.StringValue),
            TomlTable => new Val("table", string.Empty),
            TomlArray => new Val("array", string.Empty),
            _ => new Val("unknown:" + current?.GetType().Name, string.Empty),
        };
    }

    public bool RoundTrip(string text, string sameFile, string editFile)
    {
        var parser = new TomlParser();
        File.WriteAllText(sameFile, parser.Parse(text).SerializedValue);

        var edited = parser.Parse(text);
        var grouping = edited.GetSubTable("grouping");
        grouping.PutValue("auto_merge", new TomlDouble(0.85));
        grouping.PutValue("new_key", new TomlString("x"));
        File.WriteAllText(editFile, edited.SerializedValue);
        return true;
    }
}
