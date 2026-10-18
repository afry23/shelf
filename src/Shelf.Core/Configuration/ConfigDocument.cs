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
    private readonly string _keyPrefix;

    internal ConfigDocument(string sourceName, FrozenDictionary<string, ConfigNode> root, string keyPrefix = "")
    {
        SourceName = sourceName;
        _root = root;
        _keyPrefix = keyPrefix;
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

    /// <summary>
    /// The dotted keys of all values that are not tables, in a stable order. An array (including an array
    /// of tables) is one value. Used to find keys that shelf does not know.
    /// </summary>
    public IReadOnlyList<string> LeafKeys()
    {
        var keys = new List<string>();
        Collect(_root, prefix: string.Empty, keys);
        return keys;
    }

    /// <summary>
    /// The tables of an array of tables (<c>[[key]]</c>) as documents; empty if absent.
    /// Throws <see cref="ConfigException"/> if the value is not an array of tables.
    /// Keys in errors of an element read <c>key[1].name</c> (1-based).
    /// </summary>
    public IReadOnlyList<ConfigDocument> GetTables(string key)
    {
        if (Find(key) is not { } node)
        {
            return [];
        }

        var items = (System.Collections.Immutable.ImmutableArray<ConfigNode>)Expect(node, key, ConfigValueKind.Array).Value;
        var tables = new List<ConfigDocument>(items.Length);
        for (var i = 0; i < items.Length; i++)
        {
            var elementKey = $"{key}[{i + 1}]";
            tables.Add(new ConfigDocument(
                SourceName,
                Expect(items[i], elementKey, ConfigValueKind.Table).AsTable(),
                _keyPrefix + elementKey + "."));
        }

        return tables;
    }

    private static void Collect(FrozenDictionary<string, ConfigNode> table, string prefix, List<string> keys)
    {
        foreach (var (name, node) in table.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            if (node.Kind == ConfigValueKind.Table)
            {
                Collect(node.AsTable(), prefix + name + ".", keys);
            }
            else
            {
                keys.Add(prefix + name);
            }
        }
    }

    private ConfigNode Expect(ConfigNode node, string key, ConfigValueKind expected)
    {
        if (node.Kind == expected)
        {
            return node;
        }

        var fullKey = _keyPrefix + key;
        throw new ConfigException(
            $"{SourceName}: {fullKey}: expected {ConfigNode.NameOf(expected)}, found {node.KindName}",
            SourceName,
            key: fullKey);
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
