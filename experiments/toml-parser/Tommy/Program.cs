using System.Globalization;
using Proto;
using Tommy;

return Harness.Run(new TommyAdapter(), args, Console.Out, Console.Error);

internal sealed class TommyAdapter : IAdapter
{
    public object Parse(string text)
    {
        try
        {
            using var reader = new StringReader(text);
            return TOML.Parse(reader);
        }
        catch (TomlParseException e)
        {
            var first = e.SyntaxErrors.FirstOrDefault();
            throw new ParseFailure(first?.Line, first?.Column, first?.Message ?? e.Message);
        }
    }

    public Val? Get(object document, string dottedKey)
    {
        TomlNode? current = (TomlTable)document;
        foreach (var part in dottedKey.Split('.'))
        {
            if (current is not { IsTable: true } || !current.TryGetNode(part, out current))
            {
                return null;
            }
        }

        return current switch
        {
            TomlString s => new Val("string", s.Value),
            TomlInteger i => new Val("integer", i.Value.ToString(CultureInfo.InvariantCulture)),
            TomlFloat f => new Val("float", Harness.Float(f.Value)),
            TomlBoolean b => new Val("boolean", b.Value ? "true" : "false"),
            TomlDateTimeLocal or TomlDateTimeOffset => new Val("datetime", current.ToInlineToml()),
            TomlArray => new Val("array", string.Empty),
            { IsTable: true } => new Val("table", string.Empty),
            _ => new Val("unknown:" + current?.GetType().Name, string.Empty),
        };
    }

    public bool RoundTrip(string text, string sameFile, string editFile)
    {
        using (var reader = new StringReader(text))
        using (var writer = new StreamWriter(sameFile, false, new System.Text.UTF8Encoding(false)) { NewLine = "\n" })
        {
            TOML.Parse(reader).WriteTo(writer);
        }

        using (var reader = new StringReader(text))
        using (var writer = new StreamWriter(editFile, false, new System.Text.UTF8Encoding(false)) { NewLine = "\n" })
        {
            var table = TOML.Parse(reader);
            table["grouping"]["auto_merge"] = new TomlFloat { Value = 0.85 };
            table["grouping"]["new_key"] = new TomlString { Value = "x" };
            table.WriteTo(writer);
        }

        return true;
    }
}
