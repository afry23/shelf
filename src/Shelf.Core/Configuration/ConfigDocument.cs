using System.Collections.Frozen;

namespace Shelf.Core.Configuration;

/// <summary>
/// The content of one configuration file: an immutable tree addressed by dotted keys
/// (<c>grouping.auto_merge</c>). Does not expose any TOML library type.
/// </summary>
/// <remarks>
/// Each dotted segment is one table key. A key that itself contains a dot (a quoted key such as
/// <c>"a.b"</c>) cannot be addressed. Descending into a value that is not a table yields no value.
/// </remarks>
public sealed class ConfigDocument
{
    private readonly FrozenDictionary<string, ConfigNode> _root;

    internal ConfigDocument(string sourceName, FrozenDictionary<string, ConfigNode> root)
    {
        SourceName = sourceName;
        _root = root;
    }

    /// <summary>A document without any value.</summary>
    public static ConfigDocument Empty { get; } = new(string.Empty, FrozenDictionary<string, ConfigNode>.Empty);

    internal FrozenDictionary<string, ConfigNode> Root => _root;

    /// <summary>The path or name of the source this document was read from.</summary>
    public string SourceName { get; }

    /// <summary>True if a value (including a table or array) exists at the key.</summary>
    public bool Contains(string key) => Find(key) is not null;

    /// <summary>The kind of the value at the key, or null if absent.</summary>
    public ConfigValueKind? GetKind(string key) => Find(key)?.Kind;

    /// <summary>The string at the key, or null if absent. Throws <see cref="ConfigException"/> for another kind.</summary>
    public string? GetString(string key) =>
        Find(key) is { } node ? (string)Expect(node, key, ConfigValueKind.String).Value : null;

    /// <summary>The integer at the key, or null if absent. A float is not an integer.</summary>
    public long? GetInteger(string key) =>
        Find(key) is { } node ? (long)Expect(node, key, ConfigValueKind.Integer).Value : null;

    /// <summary>The float at the key, or null if absent. An integer is widened to double.</summary>
    public double? GetFloat(string key)
    {
        if (Find(key) is not { } node)
        {
            return null;
        }

        return node.Kind == ConfigValueKind.Integer
            ? (long)node.Value
            : (double)Expect(node, key, ConfigValueKind.Float).Value;
    }

    /// <summary>The boolean at the key, or null if absent.</summary>
    public bool? GetBoolean(string key) =>
        Find(key) is { } node ? (bool)Expect(node, key, ConfigValueKind.Boolean).Value : null;

    private ConfigNode Expect(ConfigNode node, string key, ConfigValueKind expected)
    {
        if (node.Kind == expected)
        {
            return node;
        }

        throw new ConfigException(
            $"{SourceName}: {key}: expected {ConfigNode.NameOf(expected)}, found {node.KindName}",
            SourceName,
            key: key);
    }

    private ConfigNode? Find(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        var segments = key.Split('.');
        if (segments.Any(segment => segment.Length == 0))
        {
            throw new ArgumentException($"'{key}' is not a valid dotted key (empty segment).", nameof(key));
        }

        FrozenDictionary<string, ConfigNode>? table = _root;
        ConfigNode? node = null;
        foreach (var segment in segments)
        {
            if (table is null || !table.TryGetValue(segment, out node))
            {
                return null;
            }

            table = node.Kind == ConfigValueKind.Table ? node.AsTable() : null;
        }

        return node;
    }
}
