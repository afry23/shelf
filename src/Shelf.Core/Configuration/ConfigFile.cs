using System.Text;

namespace Shelf.Core.Configuration;

/// <summary>Reads configuration files (TOML 1.0).</summary>
public static class ConfigFile
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// Reads and parses a file. A missing file (or missing parent directory) is an empty configuration.
    /// Any other I/O problem, invalid UTF-8 or invalid TOML throws <see cref="ConfigException"/>.
    /// </summary>
    public static ConfigDocument Load(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(path);
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
        {
            return new ConfigDocument(path, ConfigDocument.Empty.Root);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new ConfigException($"{path}: cannot read file: {e.Message}", path, innerException: e);
        }

        try
        {
            // Validates the encoding; the parser works on the bytes directly.
            _ = StrictUtf8.GetCharCount(bytes);
        }
        catch (DecoderFallbackException e)
        {
            throw new ConfigException($"{path}: file is not valid UTF-8", path, innerException: e);
        }

        return TomlConfigParser.Parse(bytes, path);
    }

    /// <summary>Parses TOML text. <paramref name="sourceName"/> is used in error messages.</summary>
    public static ConfigDocument Parse(string text, string sourceName)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(sourceName);
        return TomlConfigParser.Parse(Encoding.UTF8.GetBytes(text), sourceName);
    }
}
