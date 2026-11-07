using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Shelf.Clip;

/// <summary>
/// Reads an article from the Markdown file a web clipper saved (F-ART-2 to F-ART-5): metadata from the front matter,
/// images that the text links with a relative path copied to <c>images/</c> and the links rewritten.
/// </summary>
public static partial class MarkdownArticle
{
    /// <param name="text">The content of the Markdown file.</param>
    /// <param name="fileName">The name of the file, the last source of a title.</param>
    /// <param name="directory">The folder of the file; images are taken from below it only.</param>
    public static ClippedArticle Read(string text, string fileName, string directory)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrEmpty(directory);
        directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        var (fields, body) = FrontMatter.Split(text);

        string? First(string key) => fields.TryGetValue(key, out var v) ? v.FirstOrDefault(s => s.Length > 0) : null;

        var warnings = new List<string>();
        var images = new List<ClippedImage>();
        var rewritten = RewriteImages(body, directory, images, warnings).TrimEnd() + "\n";
        var title = First("title") ?? HeadingOf(rewritten) ?? Path.GetFileNameWithoutExtension(fileName);
        var authors = fields.TryGetValue("author", out var names) ? names.Where(n => n.Length > 0).ToList() : [];
        var source = SourceUrl.Normalize(First("source")) ?? SourceUrl.Normalize(First("url"));
        return new ClippedArticle(title, authors, First("site"), PublishedOf(First("published")), source, First("language"), rewritten, images, warnings);
    }

    private static string? HeadingOf(string markdown)
    {
        var match = FirstHeading().Match(markdown);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private static DateTimeOffset? PublishedOf(string? value)
    {
        string[] formats = ["yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ssK", "yyyy-MM-ddTHH:mm:ss.FFFK", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm"];
        return value is not null && DateTimeOffset.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date)
            ? date
            : null;
    }

    private static string RewriteImages(string markdown, string directory, List<ClippedImage> images, List<string> warnings)
    {
        var byHash = new Dictionary<string, string>(StringComparer.Ordinal);
        var byTarget = new Dictionary<string, string?>(StringComparer.Ordinal); // null: stays as it is

        string? Take(string target, bool search)
        {
            if (!byTarget.TryGetValue(target, out var link))
            {
                link = Save(target, search, directory, images, byHash, warnings);
                byTarget[target] = link;
            }

            return link;
        }

        var result = StandardImage().Replace(markdown, match =>
        {
            var target = match.Groups["src"].Value;
            var link = IsRemote(target) ? null : Take(target, search: false);
            return link is null ? match.Value : $"![{match.Groups["alt"].Value}]({link})";
        });

        return ObsidianEmbed().Replace(result, match =>
        {
            var name = match.Groups["name"].Value.Trim();
            if (!ImageExtensions.Contains(Path.GetExtension(name).TrimStart('.').ToLowerInvariant()))
            {
                return match.Value; // an embedded note or PDF is none of ours
            }

            var link = Take(name, search: true);
            var alt = match.Groups["alt"].Success ? match.Groups["alt"].Value.Trim() : name;
            return link is null ? match.Value : $"![{alt}]({link})";
        });
    }

    private static readonly HashSet<string> ImageExtensions = ["jpg", "jpeg", "png", "gif", "webp"];

    private static bool IsRemote(string target) =>
        target.Contains("://", StringComparison.Ordinal) || target.StartsWith("//", StringComparison.Ordinal)
        || target.StartsWith("data:", StringComparison.OrdinalIgnoreCase) || target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase);

    /// <returns>The link to put in the Markdown; null if the image stays as it is (a warning says why).</returns>
    private static string? Save(string target, bool search, string directory, List<ClippedImage> images, Dictionary<string, string> byHash, List<string> warnings)
    {
        var relative = Uri.UnescapeDataString(target.Split('?', '#')[0]).Replace('\\', '/');
        var full = Path.GetFullPath(relative, directory);
        if (!full.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            warnings.Add($"image not saved: {target}: outside the folder of the article");
            return null;
        }

        if (!File.Exists(full) && search)
        {
            full = Directory.EnumerateFiles(directory, Path.GetFileName(relative), SearchOption.AllDirectories).Order(StringComparer.Ordinal).FirstOrDefault() ?? full;
        }

        if (!File.Exists(full))
        {
            warnings.Add($"image not saved: {target}: no such file");
            return null;
        }

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(full);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            warnings.Add($"image not saved: {target}: {e.Message}");
            return null;
        }

        if (ImageProbe.Read(bytes) is not { } probe)
        {
            warnings.Add($"image not saved: {target}: not an image");
            return null;
        }

        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes))[..8];
        if (!byHash.TryGetValue(hash, out var name))
        {
            name = $"{images.Count + 1:D2}-{hash}.{probe.Extension}";
            images.Add(new ClippedImage(name, bytes));
            byHash[hash] = name;
        }

        return "images/" + name;
    }

    [GeneratedRegex("""!\[(?<alt>(?:[^\[\]]|\\.)*)\]\(\s*<?(?<src>[^)\s>]+)>?(?:\s+"[^"]*")?\s*\)""")]
    private static partial Regex StandardImage();

    [GeneratedRegex(@"!\[\[(?<name>[^\]|]+)(?:\|(?<alt>[^\]]*))?\]\]")]
    private static partial Regex ObsidianEmbed();

    [GeneratedRegex(@"^#\s+(.+?)\s*#*\s*$", RegexOptions.Multiline)]
    private static partial Regex FirstHeading();
}
