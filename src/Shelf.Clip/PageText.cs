using System.Text;
using System.Text.RegularExpressions;

namespace Shelf.Clip;

/// <summary>Turns the bytes of a page into text: a byte order mark, then the <c>charset</c> the page declares, then UTF-8.</summary>
public static partial class PageText
{
    public static string Decode(byte[] bytes, string? declaredCharset = null)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble))
        {
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        }

        var head = Encoding.Latin1.GetString(bytes, 0, Math.Min(bytes.Length, 2048));
        var declared = Charset().Match(head);
        var charset = declaredCharset ?? (declared.Success ? declared.Groups["name"].Value : null);
        return Open(charset).GetString(bytes);
    }

    private static Encoding Open(string? charset)
    {
        if (charset is null)
        {
            return Encoding.UTF8;
        }

        try
        {
            return Encoding.GetEncoding(charset);
        }
        catch (ArgumentException)
        {
            return Encoding.UTF8;
        }
    }

    [GeneratedRegex("""charset\s*=\s*["']?(?<name>[A-Za-z0-9_\-]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex Charset();
}
