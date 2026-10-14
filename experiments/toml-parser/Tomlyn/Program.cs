using System.Globalization;
using Proto;
using Tomlyn;
using Tomlyn.Model;
using Tomlyn.Parsing;
using Tomlyn.Serialization;
using Tomlyn.Syntax;

return Harness.Run(new TomlynAdapter(), args, Console.Out, Console.Error);

// Source-generated metadata for the untyped model: no reflection-based object mapping on the read path.
[TomlSerializable(typeof(TomlTable))]
internal sealed partial class ProtoContext : TomlSerializerContext
{
}

internal sealed class TomlynAdapter : IAdapter
{
    public object Parse(string text)
    {
        try
        {
            // TomlSerializer.Deserialize accepts duplicate keys/tables silently (also with
            // DuplicateKeyHandling.Error, verified); the syntax parser with validation reports them.
            SyntaxParser.ParseStrict(text, "input", true);
            return TomlSerializer.Deserialize(text, ProtoContext.Default.TomlTable)!;
        }
        catch (TomlException e)
        {
            throw new ParseFailure(e.Line, e.Column, e.Message);
        }
    }

    public Val? Get(object document, string dottedKey)
    {
        object? current = document;
        foreach (var part in dottedKey.Split('.'))
        {
            if (current is not TomlTable table || !table.TryGetValue(part, out current))
            {
                return null;
            }
        }

        return current switch
        {
            string s => new Val("string", s),
            long l => new Val("integer", l.ToString(CultureInfo.InvariantCulture)),
            double d => new Val("float", Harness.Float(d)),
            bool b => new Val("boolean", b ? "true" : "false"),
            TomlDateTime t => new Val("datetime", t.ToString()),
            TomlTable => new Val("table", string.Empty),
            TomlArray or TomlTableArray => new Val("array", string.Empty),
            _ => new Val("unknown:" + current?.GetType().Name, string.Empty),
        };
    }

    public bool RoundTrip(string text, string sameFile, string editFile)
    {
        // Full-fidelity syntax tree: unchanged output, then edit in place.
        var syntax = SyntaxParser.Parse(text, "input", true);
        File.WriteAllText(sameFile, syntax.ToString());

        var grouping = syntax.Tables.OfType<TableSyntax>().First(t => t.Name!.ToString().Trim() == "grouping");
        var autoMerge = grouping.Items.First(i => i.Key!.ToString().Trim() == "auto_merge");
        autoMerge.Value = new FloatValueSyntax(0.85);
        grouping.Items.Add("new_key", "x");
        File.WriteAllText(editFile, syntax.ToString());
        return true;
    }
}
