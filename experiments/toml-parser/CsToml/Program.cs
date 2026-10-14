using System.Globalization;
using System.Text;
using CsToml;
using CsToml.Error;
using CsToml.Values;
using Proto;

return Harness.Run(new CsTomlAdapter(), args, Console.Out, Console.Error);

internal sealed class CsTomlAdapter : IAdapter
{
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public object Parse(string text)
    {
        try
        {
            return CsTomlSerializer.Deserialize<TomlDocument>(Utf8.GetBytes(text));
        }
        catch (CsTomlSerializeException e) when (e.ParseExceptions is { Count: > 0 })
        {
            var first = e.ParseExceptions.First();
            var lines = first.Message.Split('\n').Select(l => l.TrimEnd('\r')).TakeWhile(l => !l.TrimStart().StartsWith("at ", StringComparison.Ordinal));
            throw new ParseFailure((int)first.LineNumber, null, string.Join(" ", lines));
        }
        catch (CsTomlException e)
        {
            throw new ParseFailure(null, null, e.Message);
        }
    }

    public Val? Get(object document, string dottedKey)
    {
        var node = ((TomlDocument)document).RootNode;
        foreach (var part in dottedKey.Split('.'))
        {
            if (node.ValueType is not (TomlValueType.Table or TomlValueType.InlineTable or TomlValueType.Empty))
            {
                return null;
            }

            try
            {
                node = node[part.AsSpan()];
            }
            catch (CsTomlException)
            {
                return null;
            }
        }

        if (node.ValueType == TomlValueType.Empty)
        {
            return node.HasNodeOnly || node.IsTableHeader ? new Val("table", string.Empty) : null;
        }

        return node.ValueType switch
        {
            TomlValueType.String => new Val("string", node.GetString()),
            TomlValueType.Integer => new Val("integer", node.GetInt64().ToString(CultureInfo.InvariantCulture)),
            TomlValueType.Float => new Val("float", Harness.Float(node.GetDouble())),
            TomlValueType.Boolean => new Val("boolean", node.GetBool() ? "true" : "false"),
            TomlValueType.OffsetDateTime or TomlValueType.LocalDateTime or TomlValueType.LocalDate or TomlValueType.LocalTime
                => new Val("datetime", node.GetObject().ToString() ?? string.Empty),
            TomlValueType.Array => new Val("array", string.Empty),
            TomlValueType.Table or TomlValueType.InlineTable => new Val("table", string.Empty),
            var other => new Val("unknown:" + other, string.Empty),
        };
    }

    public bool RoundTrip(string text, string sameFile, string editFile)
    {
        var document = CsTomlSerializer.Deserialize<TomlDocument>(Utf8.GetBytes(text));
        using (var same = CsTomlSerializer.Serialize(document))
        {
            File.WriteAllBytes(sameFile, same.ByteSpan.ToArray());
        }

        // TomlDocument has no mutation API; the closest thing is a dictionary copy (loses comments and order).
        var dictionary = document.ToDictionary<string, object>();
        var grouping = (IDictionary<string, object>)dictionary["grouping"];
        grouping["auto_merge"] = 0.85;
        grouping["new_key"] = "x";
        using var edited = CsTomlSerializer.Serialize(dictionary);
        File.WriteAllBytes(editFile, edited.ByteSpan.ToArray());
        return true;
    }
}
