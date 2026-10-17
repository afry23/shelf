using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Proto;

internal static class Auth
{
    /// <summary>--bind must be an IP address; anything else would be read as a wildcard by Kestrel's URL parsing.</summary>
    public static bool TryValidateBind(string bind, string? token, out IPAddress address, out string error)
    {
        error = "";
        if (!IPAddress.TryParse(bind, out address!))
        {
            error = $"--bind '{bind}' is not an IP address";
            return false;
        }

        if (!IPAddress.IsLoopback(address) && string.IsNullOrEmpty(token))
        {
            error = $"binding to {address} requires --token (F-WEB-2)";
            return false;
        }

        return true;
    }

    public static bool TokenMatches(string expected, string? authorizationHeader)
    {
        const string prefix = "Bearer ";
        if (authorizationHeader is null || !authorizationHeader.StartsWith(prefix, StringComparison.Ordinal))
            return false;
        var given = SHA256.HashData(Encoding.UTF8.GetBytes(authorizationHeader[prefix.Length..]));
        var want = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        return CryptographicOperations.FixedTimeEquals(given, want);
    }
}
