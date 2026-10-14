using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Shelf.Core.Configuration;

/// <summary>An immutable value in the configuration tree. Independent of the TOML library.</summary>
internal sealed class ConfigNode
{
    private ConfigNode(ConfigValueKind kind, object value)
    {
        Kind = kind;
        Value = value;
    }

    public ConfigValueKind Kind { get; }

    /// <summary>string, long, double, bool, string (DateTime kind), ImmutableArray&lt;ConfigNode&gt;, or FrozenDictionary&lt;string, ConfigNode&gt;.</summary>
    public object Value { get; }

    public static ConfigNode String(string value) => new(ConfigValueKind.String, value);

    public static ConfigNode Integer(long value) => new(ConfigValueKind.Integer, value);

    public static ConfigNode Float(double value) => new(ConfigValueKind.Float, value);

    public static ConfigNode Boolean(bool value) => new(ConfigValueKind.Boolean, value);

    public static ConfigNode DateTime(string text) => new(ConfigValueKind.DateTime, text);

    public static ConfigNode Array(ImmutableArray<ConfigNode> items) => new(ConfigValueKind.Array, items);

    public static ConfigNode Table(FrozenDictionary<string, ConfigNode> entries) => new(ConfigValueKind.Table, entries);

    public FrozenDictionary<string, ConfigNode> AsTable() => (FrozenDictionary<string, ConfigNode>)Value;

    /// <summary>The lower-case kind name used in error messages.</summary>
    public string KindName => NameOf(Kind);

    public static string NameOf(ConfigValueKind kind) => kind switch
    {
        ConfigValueKind.String => "string",
        ConfigValueKind.Integer => "integer",
        ConfigValueKind.Float => "float",
        ConfigValueKind.Boolean => "boolean",
        ConfigValueKind.DateTime => "datetime",
        ConfigValueKind.Array => "array",
        _ => "table",
    };
}
