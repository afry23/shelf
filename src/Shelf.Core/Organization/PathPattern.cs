using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Shelf.Core.Grouping;

namespace Shelf.Core.Organization;

/// <summary>What a work file contributes to a pattern (F-ORG-2).</summary>
/// <param name="Authors">All authors as written; only the first one names folders (F-ORG-4).</param>
/// <param name="FirstTag">The first tag of the work, if any.</param>
/// <param name="Extension">The file extension with or without the dot.</param>
/// <param name="CiteKey">The cite key of the work; <c>{cite_key}</c> is the title if there is none (F-PAP-10).</param>
public sealed record PatternInput(
    string Title,
    IReadOnlyList<string> Authors,
    int? Year,
    string? Series,
    double? SeriesIndex,
    string? Language,
    string? FirstTag,
    string Extension,
    string? CiteKey = null);

/// <summary>The pattern is not usable; the message names the problem.</summary>
public sealed class PatternException(string message) : Exception(message);

/// <summary>
/// A path pattern such as <c>{author_sort}/{title} ({year})/{title}.{ext}</c> (F-ORG-1, F-ORG-2): parts separated by
/// '/', placeholders in braces. Rendering yields a path relative to the library root with '/' separators.
/// </summary>
/// <remarks>
/// Rules that the PRD leaves open: a placeholder without a value renders as nothing; brackets left empty by that
/// are removed ("Title ()" becomes "Title"); a folder part that stays empty is left out; an empty file name becomes
/// "_". The last part must end with <c>.{ext}</c> so that a long title is shortened before the extension, never
/// through it. From <see cref="EtAlFrom"/> authors on, <c>{author}</c> and <c>{author_sort}</c> end with " et al"
/// (F-ORG-4); the final dot of "et al." is dropped like any trailing dot of a path part (F-ORG-3), because Windows
/// cannot name a folder "Fowler, Martin et al.".
/// </remarks>
public sealed partial class PathPattern
{
    public const string DefaultText = "{author_sort}/{title} ({year})/{title}.{ext}";

    /// <summary>The default for papers (F-PAP-10): by year, named after the key that the paper is cited with.</summary>
    public const string PaperDefaultText = "papers/{year}/{cite_key}/{cite_key}.{ext}";
    public const int EtAlFrom = 4;
    private const string ExtSuffix = ".{ext}";

    private static readonly HashSet<string> Placeholders =
    [
        "author_sort", "author", "title", "year", "series", "series_index", "language", "first_tag", "cite_key", "ext",
    ];

    private readonly string[] _folders;
    private readonly string _file;

    private PathPattern(string text, string[] folders, string file)
    {
        Text = text;
        _folders = folders;
        _file = file;
    }

    public static PathPattern Default { get; } = Parse(DefaultText);

    public static PathPattern PaperDefault { get; } = Parse(PaperDefaultText);

    public string Text { get; }

    /// <exception cref="PatternException"/>
    public static PathPattern Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new PatternException("must not be empty");
        }

        if (text.StartsWith('/') || text.StartsWith('\\') || Path.IsPathRooted(text) || (text.Length > 1 && text[1] == ':'))
        {
            throw new PatternException("must be a relative path");
        }

        var parts = text.Split('/');
        if (parts.Any(p => p == ".."))
        {
            throw new PatternException("must not contain '..'");
        }

        CheckBraces(text);
        var last = parts[^1];
        if (!last.EndsWith(ExtSuffix, StringComparison.Ordinal))
        {
            throw new PatternException($"the last part must end with '{ExtSuffix}'");
        }

        return new PathPattern(text, parts[..^1], last[..^ExtSuffix.Length]);
    }

    /// <summary>The relative path of one file, e.g. <c>Fowler, Martin/Patterns (2002)/Patterns.epub</c>.</summary>
    public string Render(PatternInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var extension = PathSanitizer.Segment(input.Extension.TrimStart('.').ToLowerInvariant());
        var values = Values(input, extension);

        var segments = new List<string>();
        foreach (var folder in _folders)
        {
            var text = Tidy(Substitute(folder, values));
            if (text.Length > 0)
            {
                segments.Add(PathSanitizer.Segment(text));
            }
        }

        var stem = Tidy(Substitute(_file, values));
        var room = PathSanitizer.MaxBytes - 1 - Encoding.UTF8.GetByteCount(extension);
        segments.Add(PathSanitizer.Segment(stem, room) + "." + extension);
        return string.Join('/', segments);
    }

    public override string ToString() => Text;

    private static Dictionary<string, string> Values(PatternInput input, string extension)
    {
        var first = input.Authors.Count > 0 && !string.IsNullOrWhiteSpace(input.Authors[0]) ? input.Authors[0] : AuthorNames.UnknownAuthor;
        var etAl = input.Authors.Count >= EtAlFrom ? " et al" : string.Empty;
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["title"] = input.Title,
            ["author"] = first + etAl,
            ["author_sort"] = AuthorNames.Parse(first).SortKey + etAl,
            ["year"] = input.Year?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            ["series"] = input.Series ?? string.Empty,
            ["series_index"] = input.SeriesIndex?.ToString("0.##", CultureInfo.InvariantCulture) ?? string.Empty,
            ["language"] = input.Language ?? string.Empty,
            ["first_tag"] = input.FirstTag ?? string.Empty,
            ["cite_key"] = string.IsNullOrWhiteSpace(input.CiteKey) ? input.Title : input.CiteKey,
            ["ext"] = extension,
        };
    }

    private static string Substitute(string part, Dictionary<string, string> values) =>
        PlaceholderRegex().Replace(part, m => values[m.Groups[1].Value]);

    /// <summary>Removes brackets that a missing value left empty, then collapses and trims white space.</summary>
    private static string Tidy(string text)
    {
        string previous;
        do
        {
            previous = text;
            text = EmptyBrackets().Replace(text, string.Empty);
        }
        while (text != previous);

        return string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static void CheckBraces(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            switch (text[i])
            {
                case '}':
                    throw new PatternException("unbalanced braces");
                case '{':
                    var close = text.IndexOf('}', i);
                    if (close < 0 || text.IndexOf('{', i + 1, close - i - 1) >= 0)
                    {
                        throw new PatternException("unbalanced braces");
                    }

                    var name = text[(i + 1)..close];
                    if (!Placeholders.Contains(name))
                    {
                        throw new PatternException($"unknown placeholder '{{{name}}}'");
                    }

                    i = close;
                    break;
            }
        }
    }

    [GeneratedRegex(@"\{([a-z_]+)\}")]
    private static partial Regex PlaceholderRegex();

    [GeneratedRegex(@"\(\s*\)|\[\s*\]")]
    private static partial Regex EmptyBrackets();
}
