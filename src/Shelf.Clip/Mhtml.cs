using System.Text;

namespace Shelf.Clip;

/// <summary>A web page saved as MHTML: the HTML and the files it uses, by the address they had (F-ART-3).</summary>
public sealed class Mhtml
{
    private readonly Dictionary<string, byte[]> _parts = new(StringComparer.Ordinal);

    private Mhtml(string html, string? location)
    {
        Html = html;
        Location = location;
    }

    public string Html { get; }

    /// <summary>The address of the page itself (the <c>Snapshot-Content-Location</c> or the location of the first part).</summary>
    public string? Location { get; }

    public byte[]? Find(string address) => _parts.TryGetValue(address, out var bytes) ? bytes : null;

    /// <summary>True if the text starts like an MIME message and not like HTML.</summary>
    public static bool Looks(string text) => text.StartsWith("From:", StringComparison.OrdinalIgnoreCase)
        || text.StartsWith("MIME-Version:", StringComparison.OrdinalIgnoreCase)
        || text.StartsWith("Subject:", StringComparison.OrdinalIgnoreCase);

    /// <exception cref="FormatException">There is no HTML part.</exception>
    public static Mhtml Parse(byte[] data)
    {
        var text = Encoding.Latin1.GetString(data);
        var (headers, body) = Split(text);
        var contentType = headers.GetValueOrDefault("content-type") ?? string.Empty;
        var boundary = Parameter(contentType, "boundary") ?? throw new FormatException("not a multipart MHTML file: no boundary");
        string? html = null;
        string? location = headers.GetValueOrDefault("snapshot-content-location");
        var parts = new List<(string? Location, byte[] Bytes)>();
        foreach (var raw in body.Split("--" + boundary).Skip(1))
        {
            if (raw.StartsWith("--", StringComparison.Ordinal))
            {
                break;
            }

            var (partHeaders, partBody) = Split(raw.TrimStart('\r', '\n'));
            var type = (partHeaders.GetValueOrDefault("content-type") ?? "text/plain").Split(';')[0].Trim().ToLowerInvariant();
            var bytes = Decode(partBody, partHeaders.GetValueOrDefault("content-transfer-encoding"));
            var where = partHeaders.GetValueOrDefault("content-location");
            if (type == "text/html" && html is null)
            {
                var charset = Parameter(partHeaders.GetValueOrDefault("content-type") ?? string.Empty, "charset") ?? "utf-8";
                html = Encode(charset).GetString(bytes);
                location ??= where;
            }
            else if (where is not null)
            {
                parts.Add((where, bytes));
            }
        }

        var result = new Mhtml(html ?? throw new FormatException("no HTML part in the MHTML file"), location);
        foreach (var (where, bytes) in parts)
        {
            result._parts.TryAdd(where!, bytes);
        }

        return result;
    }

    private static Encoding Encode(string charset)
    {
        try
        {
            return Encoding.GetEncoding(charset);
        }
        catch (ArgumentException)
        {
            return Encoding.UTF8;
        }
    }

    private static (Dictionary<string, string> Headers, string Body) Split(string text)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var end = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        var gap = 4;
        if (end < 0)
        {
            end = text.IndexOf("\n\n", StringComparison.Ordinal);
            gap = 2;
        }

        if (end < 0)
        {
            return (headers, string.Empty);
        }

        string? name = null;
        foreach (var line in text[..end].Split('\n').Select(l => l.TrimEnd('\r')))
        {
            if ((line.StartsWith(' ') || line.StartsWith('\t')) && name is not null)
            {
                headers[name] += " " + line.Trim();
                continue;
            }

            var colon = line.IndexOf(':');
            if (colon > 0)
            {
                name = line[..colon].Trim();
                headers[name] = line[(colon + 1)..].Trim();
            }
        }

        return (headers, text[(end + gap)..]);
    }

    private static string? Parameter(string header, string name)
    {
        foreach (var piece in header.Split(';').Skip(1))
        {
            var pair = piece.Split('=', 2);
            if (pair.Length == 2 && pair[0].Trim().Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return pair[1].Trim().Trim('"');
            }
        }

        return null;
    }

    private static byte[] Decode(string body, string? encoding)
    {
        switch (encoding?.Trim().ToLowerInvariant())
        {
            case "base64":
                try
                {
                    return Convert.FromBase64String(new string(body.Where(c => !char.IsWhiteSpace(c)).ToArray()));
                }
                catch (FormatException)
                {
                    return [];
                }

            case "quoted-printable":
                return QuotedPrintable(body);
            default:
                return Encoding.Latin1.GetBytes(body.TrimEnd('\r', '\n'));
        }
    }

    private static byte[] QuotedPrintable(string body)
    {
        var bytes = new List<byte>(body.Length);
        for (var i = 0; i < body.Length; i++)
        {
            var c = body[i];
            if (c != '=')
            {
                bytes.Add((byte)c);
                continue;
            }

            if (i + 1 < body.Length && body[i + 1] is '\r' or '\n')
            {
                i += body[i + 1] == '\r' && i + 2 < body.Length && body[i + 2] == '\n' ? 2 : 1; // soft line break
                continue;
            }

            if (i + 2 < body.Length && byte.TryParse(body.AsSpan(i + 1, 2), System.Globalization.NumberStyles.HexNumber, null, out var value))
            {
                bytes.Add(value);
                i += 2;
            }
            else
            {
                bytes.Add((byte)c);
            }
        }

        return [.. bytes];
    }
}
