using System.Globalization;
using System.Text;
using CsToml;
using CsToml.Error;
using CsToml.Values;

namespace Proto;

internal static class ConfigCommand
{
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static int Get(string file, string dottedKey, TextWriter stdout, TextWriter stderr)
    {
        TomlDocument document;
        try
        {
            document = CsTomlSerializer.Deserialize<TomlDocument>(Utf8.GetBytes(File.ReadAllText(file)));
        }
        catch (CsTomlSerializeException e) when (e.ParseExceptions is { Count: > 0 })
        {
            var first = e.ParseExceptions.First();
            stderr.WriteLine($"{file}: line {first.LineNumber}: {first.Message.Split('\n')[0].TrimEnd('\r')}");
            return 1;
        }
        catch (CsTomlException e)
        {
            stderr.WriteLine($"{file}: {e.Message}");
            return 1;
        }

        var node = document.RootNode;
        foreach (var part in dottedKey.Split('.'))
        {
            if (node.ValueType is not (TomlValueType.Table or TomlValueType.InlineTable or TomlValueType.Empty))
            {
                stderr.WriteLine($"key '{dottedKey}' not found");
                return 1;
            }

            try
            {
                node = node[part.AsSpan()];
            }
            catch (CsTomlException)
            {
                stderr.WriteLine($"key '{dottedKey}' not found");
                return 1;
            }
        }

        switch (node.ValueType)
        {
            case TomlValueType.String:
                stdout.WriteLine("string:" + node.GetString());
                return 0;
            case TomlValueType.Integer:
                stdout.WriteLine("integer:" + node.GetInt64().ToString(CultureInfo.InvariantCulture));
                return 0;
            case TomlValueType.Float:
                stdout.WriteLine("float:" + node.GetDouble().ToString("R", CultureInfo.InvariantCulture));
                return 0;
            case TomlValueType.Boolean:
                stdout.WriteLine("boolean:" + (node.GetBool() ? "true" : "false"));
                return 0;
            default:
                stderr.WriteLine($"key '{dottedKey}' is not a scalar ({node.ValueType})");
                return 1;
        }
    }
}
