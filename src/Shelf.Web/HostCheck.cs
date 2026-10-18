using System.Net;

namespace Shelf.Web;

/// <summary>
/// Which <c>Host</c> a server on a loopback address answers to. A web page of another site can make the browser send
/// requests to <c>127.0.0.1</c> under its own host name (DNS rebinding); refusing every other name closes that way in
/// for a server that has no token.
/// </summary>
internal static class HostCheck
{
    /// <summary><c>localhost</c> or a loopback IP address, with or without a port. A request without a host passes.</summary>
    public static bool IsLoopbackHost(string? host)
    {
        if (string.IsNullOrEmpty(host))
        {
            return true;
        }

        var name = StripPort(host);
        if (name.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (name.Length > 1 && name[0] == '[' && name[^1] == ']')
        {
            name = name[1..^1];
        }

        return IPAddress.TryParse(name, out var address) && IPAddress.IsLoopback(address);
    }

    private static string StripPort(string host)
    {
        if (host[0] == '[')
        {
            var close = host.IndexOf(']', StringComparison.Ordinal);
            return close < 0 ? host : host[..(close + 1)];
        }

        var colon = host.LastIndexOf(':');
        return colon < 0 || host.IndexOf(':', StringComparison.Ordinal) != colon ? host : host[..colon];
    }
}
