namespace Shelf.Core.Configuration;

/// <summary>
/// A configuration file could not be read, is not valid TOML, or a value has a different type than requested.
/// </summary>
public sealed class ConfigException : Exception
{
    public ConfigException(string message, string sourceName, int? line = null, int? column = null, string? key = null, Exception? innerException = null)
        : base(message, innerException)
    {
        SourceName = sourceName;
        Line = line;
        Column = column;
        Key = key;
    }

    /// <summary>The path or name of the source, as given to <see cref="ConfigFile.Load"/> or <see cref="ConfigFile.Parse"/>.</summary>
    public string SourceName { get; }

    /// <summary>1-based line of a syntax error, if known.</summary>
    public int? Line { get; }

    /// <summary>1-based column of a syntax error, if known.</summary>
    public int? Column { get; }

    /// <summary>The dotted key of a type mismatch, if the error concerns a value.</summary>
    public string? Key { get; }
}
