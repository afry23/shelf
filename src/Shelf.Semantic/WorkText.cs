using System.Security.Cryptography;
using System.Text;

namespace Shelf.Semantic;

/// <summary>The text that stands for a whole work in the work-level search (F-SEM-3).</summary>
public static class WorkText
{
    /// <summary>
    /// Title, subtitle, description, abstract and tags, one per line. The abstract of a paper (F-PAP-7) is its description: it
    /// says what the paper is about, which its title often does not. The table of contents joins them when the extraction
    /// provides it (the MCP slice); the work is then embedded again because its hash changes. A work without an abstract
    /// has the same text, and so the same hash, as before the abstract was added.
    /// </summary>
    public static string Build(string title, string? subtitle, string? description, string? abstractText, IReadOnlyList<string> tags)
    {
        var builder = new StringBuilder(title);
        Append(builder, subtitle);
        Append(builder, description);
        Append(builder, abstractText);
        if (tags.Count > 0)
        {
            Append(builder, "Tags: " + string.Join(", ", tags));
        }

        return builder.ToString();
    }

    /// <summary>Title and subtitle, one per line: what the title search (F-SEM-10) compares the query with.</summary>
    public static string BuildTitle(string title, string? subtitle)
    {
        var builder = new StringBuilder(title);
        Append(builder, subtitle);
        return builder.ToString();
    }

    public static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private static void Append(StringBuilder builder, string? line)
    {
        if (!string.IsNullOrWhiteSpace(line))
        {
            builder.Append('\n').Append(line.Trim());
        }
    }
}
