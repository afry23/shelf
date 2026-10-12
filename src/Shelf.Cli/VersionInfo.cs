using System.Reflection;

namespace Shelf.Cli;

internal static class VersionInfo
{
    /// <summary>The product version without the <c>+commit</c> suffix, from this assembly (not the entry assembly).</summary>
    public static string Get() => Format(
        typeof(VersionInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

    public static string Format(string? informationalVersion)
    {
        if (string.IsNullOrEmpty(informationalVersion))
        {
            return "0.0.0";
        }

        var plus = informationalVersion.IndexOf('+', StringComparison.Ordinal);
        return plus >= 0 ? informationalVersion[..plus] : informationalVersion;
    }
}
