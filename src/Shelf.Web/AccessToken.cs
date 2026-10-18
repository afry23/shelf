using System.Security.Cryptography;
using System.Text;

namespace Shelf.Web;

/// <summary>The access token of the web server: a bearer header for tools, a cookie for the browser.</summary>
internal static class AccessToken
{
    public const string CookieName = "shelf_token";
    public const string QueryName = "token";

    private const string BearerScheme = "Bearer";

    /// <summary>Compares in constant time, whatever the lengths (both sides are hashed first).</summary>
    public static bool Matches(string expected, string? given)
    {
        if (given is null)
        {
            return false;
        }

        var want = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        var have = SHA256.HashData(Encoding.UTF8.GetBytes(given));
        return CryptographicOperations.FixedTimeEquals(want, have);
    }

    /// <summary>The value after <c>Bearer </c> (the scheme is case-insensitive, the token is not), or null for any other header.</summary>
    public static string? FromAuthorization(string? header)
    {
        if (header is null || header.Length <= BearerScheme.Length + 1 || header[BearerScheme.Length] != ' ')
        {
            return null;
        }

        return header.StartsWith(BearerScheme, StringComparison.OrdinalIgnoreCase) ? header[(BearerScheme.Length + 1)..] : null;
    }
}
