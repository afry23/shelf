using System.Net;

namespace Shelf.Web;

/// <summary>Where the server may listen (F-WEB-1, F-WEB-2).</summary>
public static class BindRules
{
    /// <summary>
    /// <paramref name="bind"/> must be an IP address (anything else would be read as a wildcard by Kestrel's URL parsing,
    /// ADR 0007), and an address that is not loopback needs a token.
    /// </summary>
    public static bool TryValidate(string bind, string? token, out IPAddress address, out string error)
    {
        ArgumentNullException.ThrowIfNull(bind);

        error = string.Empty;
        // IPAddress.TryParse also reads "0", "127.1" and "2130706433" as addresses; only the written-out forms count here.
        var written = bind.Contains(':', StringComparison.Ordinal) || bind.Count(c => c == '.') == 3;
        address = IPAddress.None;
        if (!written || !IPAddress.TryParse(bind, out var parsed))
        {
            error = $"--bind '{bind}' is not an IP address";
            return false;
        }

        address = parsed;

        if (!IPAddress.IsLoopback(address) && string.IsNullOrEmpty(token))
        {
            error = $"binding to {address} requires --token (F-WEB-2)";
            return false;
        }

        return true;
    }
}
