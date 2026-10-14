namespace Shelf.Core.Configuration;

/// <summary>The kind of a value in a TOML configuration file.</summary>
public enum ConfigValueKind
{
    String,
    Integer,
    Float,
    Boolean,
    DateTime,
    Array,
    Table,
}
