namespace Shelf.Clip;

/// <summary>The address that identifies an article (F-ART-6): no fragment, no tracking parameters, host in lower case.</summary>
public static class SourceUrl
{
    /// <summary>The normalized address, or null if <paramref name="url"/> is no absolute http(s) address.</summary>
    public static string? Normalize(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
        {
            return null;
        }

        var kept = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(pair => !IsTracking(pair.Split('=', 2)[0]))
            .ToList();
        var builder = new UriBuilder(uri) { Fragment = string.Empty, Query = string.Join('&', kept) };
        return builder.Uri.GetComponents(UriComponents.AbsoluteUri, UriFormat.UriEscaped);
    }

    private static bool IsTracking(string name) =>
        name.StartsWith("utm_", StringComparison.OrdinalIgnoreCase)
        || name.Equals("fbclid", StringComparison.OrdinalIgnoreCase)
        || name.Equals("gclid", StringComparison.OrdinalIgnoreCase)
        || name.Equals("mc_cid", StringComparison.OrdinalIgnoreCase)
        || name.Equals("mc_eid", StringComparison.OrdinalIgnoreCase);
}
