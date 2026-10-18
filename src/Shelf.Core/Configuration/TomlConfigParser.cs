using System.Collections;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Globalization;
using CsToml;
using CsToml.Error;

namespace Shelf.Core.Configuration;

/// <summary>
/// Maps TOML text to shelf's own configuration tree. The only code that references the TOML library
/// (ADR 0003); replacing the library means replacing this file.
/// </summary>
internal static class TomlConfigParser
{
    // Pinned to TOML 1.0.0 so that a library default change cannot start accepting 1.1-only syntax.
    private static readonly CsTomlSerializerOptions Options =
        CsTomlSerializerOptions.Default with { Spec = TomlSpec.Version100 };

    public static ConfigDocument Parse(ReadOnlySpan<byte> utf8, string sourceName)
    {
        try
        {
            return ParseCore(utf8, sourceName);
        }
        catch (Exception e) when (e is not ConfigException and not OutOfMemoryException)
        {
            // Only ConfigException may escape, whatever the library throws.
            throw new ConfigException($"{sourceName}: {FirstLines(e.Message)}", sourceName, innerException: e);
        }
    }

    private static ConfigDocument ParseCore(ReadOnlySpan<byte> utf8, string sourceName)
    {
        // The config template of `init` is all comments. Starting the TOML library costs about 10 ms, which is a
        // tenth of the time a search may take, so a file without any content does not need it.
        if (HasNoContent(utf8))
        {
            return new ConfigDocument(sourceName, FrozenDictionary<string, ConfigNode>.Empty);
        }

        TomlDocument document;
        try
        {
            document = CsTomlSerializer.Deserialize<TomlDocument>(utf8, Options);
        }
        catch (CsTomlSerializeException e) when (e.ParseExceptions is { Count: > 0 })
        {
            var first = e.ParseExceptions.First();
            throw SyntaxError(first, sourceName);
        }
        catch (CsTomlParseException e)
        {
            throw SyntaxError(e, sourceName);
        }
        catch (CsTomlException e)
        {
            throw new ConfigException($"{sourceName}: {FirstLines(e.Message)}", sourceName, innerException: e);
        }

        IDictionary<string, object> root;
        try
        {
            root = document.ToDictionary<string, object>();
        }
        catch (CsTomlException e)
        {
            throw new ConfigException($"{sourceName}: {FirstLines(e.Message)}", sourceName, innerException: e);
        }

        return new ConfigDocument(sourceName, MapTable((IDictionary)root));
    }

    /// <summary>
    /// True if the text is plain ASCII and every line is blank or a comment. Anything else (control characters,
    /// non-ASCII bytes, a byte order mark) is left to the TOML library, which also judges validity.
    /// </summary>
    internal static bool HasNoContent(ReadOnlySpan<byte> utf8)
    {
        var inComment = false;
        foreach (var b in utf8)
        {
            if (b >= 0x80 || (b < 0x20 && b is not ((byte)'\t' or (byte)'\n' or (byte)'\r')) || b == 0x7F)
            {
                return false;
            }

            if (b == (byte)'\n')
            {
                inComment = false;
            }
            else if (!inComment && b == (byte)'#')
            {
                inComment = true;
            }
            else if (!inComment && b is not ((byte)' ' or (byte)'\t' or (byte)'\r'))
            {
                return false;
            }
        }

        return true;
    }

    private static ConfigException SyntaxError(CsTomlParseException e, string sourceName)
    {
        // The library message starts with a sentence naming the line, then the reason, then a stack trace.
        var reason = FirstLines(e.InnerException?.Message ?? e.Message);
        const string prefix = "CsToml.Error.CsTomlException: ";
        var at = reason.LastIndexOf(prefix, StringComparison.Ordinal);
        if (at >= 0)
        {
            reason = reason[(at + prefix.Length)..];
        }

        var line = (int)Math.Min(e.LineNumber, int.MaxValue);
        return new ConfigException($"{sourceName}:{line}: {reason}", sourceName, line: line, innerException: e);
    }

    /// <summary>The message without stack trace lines (the library embeds the trace in the message).</summary>
    private static string FirstLines(string message)
    {
        var lines = message.Split('\n')
            .Select(l => l.TrimEnd('\r'))
            .TakeWhile(l => !l.TrimStart().StartsWith("at ", StringComparison.Ordinal));
        return string.Join(' ', lines).Trim();
    }

    private static FrozenDictionary<string, ConfigNode> MapTable(IDictionary table)
    {
        var entries = new Dictionary<string, ConfigNode>(table.Count, StringComparer.Ordinal);
        foreach (DictionaryEntry entry in table)
        {
            entries[(string)entry.Key] = MapValue(entry.Value);
        }

        return entries.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private static ConfigNode MapValue(object? value) => value switch
    {
        string s => ConfigNode.String(s),
        long l => ConfigNode.Integer(l),
        double d => ConfigNode.Float(d),
        bool b => ConfigNode.Boolean(b),
        DateTimeOffset dto => ConfigNode.DateTime(dto.ToString("O", CultureInfo.InvariantCulture)),
        DateTime dt => ConfigNode.DateTime(dt.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture)),
        DateOnly d => ConfigNode.DateTime(d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
        TimeOnly t => ConfigNode.DateTime(t.ToString("HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture)),
        IDictionary table => ConfigNode.Table(MapTable(table)),
        IEnumerable items => ConfigNode.Array([.. items.Cast<object?>().Select(MapValue)]),
        _ => throw new InvalidOperationException($"Unsupported TOML value type {value?.GetType().FullName ?? "null"}."),
    };
}
