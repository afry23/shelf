using Shelf.Core.Organization;

namespace Shelf.Core.Configuration;

/// <summary>Thresholds of the grouping cascade (PRD 6.3, F-GRP-6).</summary>
/// <param name="AutoMerge">Confidence from which a match is merged automatically.</param>
/// <param name="Review">Confidence from which a match becomes a review entry instead of a new work.</param>
/// <param name="MaxHamming">Largest SimHash Hamming distance that still counts as a match (needs review).</param>
/// <param name="AutoHamming">Largest SimHash Hamming distance that counts as a sure match.</param>
/// <param name="ProbableHamming">Largest SimHash Hamming distance between two works that makes them probable duplicates (F-DUP-3).</param>
/// <param name="ProbableCosine">Smallest cosine similarity of the vectors of two works that makes them probable duplicates (F-DUP-3).</param>
public sealed record GroupingSettings(double AutoMerge, double Review, int MaxHamming, int AutoHamming, int ProbableHamming = 3, double ProbableCosine = 0.95)
{
    public static GroupingSettings Default { get; } = new(0.9, 0.5, 5, 3);
}

/// <summary>How <c>shelf organize</c> names folders and files (F-ORG-1, F-ORG-2).</summary>
public sealed record OrganizeSettings(PathPattern Pattern)
{
    public static OrganizeSettings Default { get; } = new(PathPattern.Default);
}

/// <summary>The embedding model for semantic search (F-SEM-1, F-SEM-8).</summary>
public sealed record SemanticSettings(string Model)
{
    public static SemanticSettings Default { get; } = new("multilingual-e5-small");
}

/// <summary>The MCP server (PRD 7.2).</summary>
/// <param name="MaxChars">Hard maximum for <c>maxChars</c> of the tool <c>read_passage</c>.</param>
/// <param name="ReadOnly">The tools that change something (<c>add_tags</c>, <c>remove_tags</c>, <c>add_note</c>) are not offered.</param>
public sealed record McpSettings(int MaxChars, bool ReadOnly = false)
{
    public const int DefaultMaxChars = 20_000;
    public const int LargestMaxChars = 1_000_000;

    /// <summary>The port of <c>shelf mcp --http</c>, next to the 8420 of the web server.</summary>
    public const int DefaultHttpPort = 8421;

    public static McpSettings Default { get; } = new(DefaultMaxChars);
}

/// <summary>The reader of the web interface (F-RDR-5).</summary>
/// <param name="PreferredFormat">The format that <c>Read</c> opens when a work has more than one: <c>epub</c> or <c>pdf</c>.</param>
public sealed record ReaderSettings(string PreferredFormat)
{
    public static ReaderSettings Default { get; } = new("epub");
}

/// <summary>The web server (F-WEB-1, F-WEB-2). Set in the global config only: a token must not travel with a library folder.</summary>
/// <param name="Port">The port; 0 lets the system choose one.</param>
/// <param name="Bind">The IP address to listen on; a loopback address unless a token is set.</param>
/// <param name="Token">The access token; null when none is set (an empty value counts as none).</param>
public sealed record WebSettings(int Port, string Bind, string? Token)
{
    public const int DefaultPort = 8420;
    public const string DefaultBind = "127.0.0.1";

    public static WebSettings Default { get; } = new(DefaultPort, DefaultBind, null);
}

/// <summary>A library named in the global config (F-LIB-1).</summary>
public sealed record LibraryEntry(string Name, string Path);

/// <summary>
/// The settings of one run: built-in defaults, overridden by the global config, overridden by the
/// per-library config (PRD 9.2). Keys that shelf does not know are not an error; they produce a warning.
/// </summary>
public sealed class ShelfSettings
{
    private const string AutoMergeKey = "grouping.auto_merge";
    private const string ReviewKey = "grouping.review";
    private const string MaxHammingKey = "grouping.max_hamming";
    private const string AutoHammingKey = "grouping.auto_hamming";
    private const string ProbableHammingKey = "grouping.probable_hamming";
    private const string ProbableCosineKey = "grouping.probable_cosine";
    private const string PatternKey = "organize.pattern";
    private const string ModelKey = "semantic.model";
    private const string McpMaxCharsKey = "mcp.max_chars";
    private const string McpReadOnlyKey = "mcp.read_only";
    private const string PreferredFormatKey = "reader.preferred_format";
    private const string WebPortKey = "web.port";
    private const string WebBindKey = "web.bind";
    private const string WebTokenKey = "web.token";
    private const string DefaultLibraryKey = "default_library";
    private const string LibrariesKey = "libraries";

    private static readonly HashSet<string> LibraryKeys = [AutoMergeKey, ReviewKey, MaxHammingKey, AutoHammingKey, ProbableHammingKey, ProbableCosineKey, PatternKey, ModelKey, McpMaxCharsKey, McpReadOnlyKey, PreferredFormatKey];
    private static readonly HashSet<string> GlobalKeys = [AutoMergeKey, ReviewKey, MaxHammingKey, AutoHammingKey, ProbableHammingKey, ProbableCosineKey, PatternKey, ModelKey, McpMaxCharsKey, McpReadOnlyKey, PreferredFormatKey, WebPortKey, WebBindKey, WebTokenKey, DefaultLibraryKey, LibrariesKey];

    private ShelfSettings(
        GroupingSettings grouping,
        OrganizeSettings organize,
        SemanticSettings semantic,
        McpSettings mcp,
        ReaderSettings reader,
        WebSettings web,
        IReadOnlyList<LibraryEntry> libraries,
        string? defaultLibrary,
        IReadOnlyList<string> warnings)
    {
        Grouping = grouping;
        Organize = organize;
        Semantic = semantic;
        Mcp = mcp;
        Reader = reader;
        Web = web;
        Libraries = libraries;
        DefaultLibrary = defaultLibrary;
        Warnings = warnings;
    }

    public GroupingSettings Grouping { get; }

    public OrganizeSettings Organize { get; }

    public SemanticSettings Semantic { get; }

    public McpSettings Mcp { get; }

    public ReaderSettings Reader { get; }

    public WebSettings Web { get; }

    /// <summary>Libraries from <c>[[libraries]]</c> of the global config; the per-library config cannot add any.</summary>
    public IReadOnlyList<LibraryEntry> Libraries { get; }

    /// <summary>The <c>default_library</c> name of the global config, if any.</summary>
    public string? DefaultLibrary { get; }

    /// <summary>One line per unknown key, as <c>&lt;source&gt;: unknown key '&lt;key&gt;'</c>.</summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>Settings without any config file.</summary>
    public static ShelfSettings Defaults { get; } = new(GroupingSettings.Default, OrganizeSettings.Default, SemanticSettings.Default, McpSettings.Default, ReaderSettings.Default, WebSettings.Default, [], null, []);

    /// <exception cref="ConfigException">A known key has the wrong type or an invalid value.</exception>
    public static ShelfSettings Resolve(ConfigDocument? global, ConfigDocument? library)
    {
        var warnings = new List<string>();
        if (global is not null)
        {
            Warn(global, GlobalKeys, warnings);
        }

        if (library is not null)
        {
            Warn(library, LibraryKeys, warnings);
        }

        var autoMerge = GroupingSettings.Default.AutoMerge;
        var review = GroupingSettings.Default.Review;
        var maxHamming = GroupingSettings.Default.MaxHamming;
        var autoHamming = GroupingSettings.Default.AutoHamming;
        var probableHamming = GroupingSettings.Default.ProbableHamming;
        var probableCosine = GroupingSettings.Default.ProbableCosine;
        var pattern = PathPattern.Default;
        var model = SemanticSettings.Default.Model;
        var maxChars = McpSettings.DefaultMaxChars;
        var readOnly = false;
        var preferredFormat = ReaderSettings.Default.PreferredFormat;
        foreach (var layer in new[] { global, library })
        {
            if (layer is null)
            {
                continue;
            }

            autoMerge = ReadThreshold(layer, AutoMergeKey) ?? autoMerge;
            review = ReadThreshold(layer, ReviewKey) ?? review;
            maxHamming = ReadHamming(layer, MaxHammingKey) ?? maxHamming;
            autoHamming = ReadHamming(layer, AutoHammingKey) ?? autoHamming;
            probableHamming = ReadHamming(layer, ProbableHammingKey) ?? probableHamming;
            probableCosine = ReadThreshold(layer, ProbableCosineKey) ?? probableCosine;
            pattern = ReadPattern(layer) ?? pattern;
            model = ReadModel(layer) ?? model;
            maxChars = ReadMaxChars(layer) ?? maxChars;
            readOnly = layer.GetBoolean(McpReadOnlyKey) ?? readOnly;
            preferredFormat = ReadPreferredFormat(layer) ?? preferredFormat;
        }

        if (review > autoMerge)
        {
            var source = (library ?? global)!.SourceName;
            throw new ConfigException($"{source}: {ReviewKey}: must not exceed {AutoMergeKey}", source, key: ReviewKey);
        }

        if (autoHamming > maxHamming)
        {
            var source = (library ?? global)!.SourceName;
            throw new ConfigException($"{source}: {AutoHammingKey}: must not exceed {MaxHammingKey}", source, key: AutoHammingKey);
        }

        var web = global is null ? WebSettings.Default : ReadWeb(global);
        var libraries = global is null ? [] : ReadLibraries(global);
        var defaultLibrary = global?.GetString(DefaultLibraryKey);
        if (defaultLibrary is not null && !libraries.Any(l => l.Name == defaultLibrary))
        {
            throw new ConfigException(
                $"{global!.SourceName}: {DefaultLibraryKey}: no library named '{defaultLibrary}'",
                global.SourceName,
                key: DefaultLibraryKey);
        }

        return new ShelfSettings(new GroupingSettings(autoMerge, review, maxHamming, autoHamming, probableHamming, probableCosine), new OrganizeSettings(pattern), new SemanticSettings(model), new McpSettings(maxChars, readOnly), new ReaderSettings(preferredFormat), web, libraries, defaultLibrary, warnings);
    }

    private static void Warn(ConfigDocument document, HashSet<string> known, List<string> warnings)
    {
        foreach (var key in document.LeafKeys())
        {
            if (!known.Contains(key))
            {
                warnings.Add($"{document.SourceName}: unknown key '{key}'");
            }
        }
    }

    private static double? ReadThreshold(ConfigDocument document, string key)
    {
        var value = document.GetFloat(key);
        if (value is < 0 or > 1)
        {
            throw new ConfigException($"{document.SourceName}: {key}: must be between 0 and 1", document.SourceName, key: key);
        }

        return value;
    }

    private static int? ReadMaxChars(ConfigDocument document)
    {
        var value = document.GetInteger(McpMaxCharsKey);
        if (value is < 1 or > McpSettings.LargestMaxChars)
        {
            throw new ConfigException(
                $"{document.SourceName}: {McpMaxCharsKey}: must be between 1 and {McpSettings.LargestMaxChars}",
                document.SourceName,
                key: McpMaxCharsKey);
        }

        return (int?)value;
    }

    private static string? ReadPreferredFormat(ConfigDocument document)
    {
        var name = document.GetString(PreferredFormatKey)?.Trim().ToLowerInvariant();
        if (name is not (null or "epub" or "pdf"))
        {
            throw new ConfigException($"{document.SourceName}: {PreferredFormatKey}: must be epub or pdf", document.SourceName, key: PreferredFormatKey);
        }

        return name;
    }

    private static WebSettings ReadWeb(ConfigDocument document)
    {
        var port = document.GetInteger(WebPortKey);
        if (port is < 0 or > 65535)
        {
            throw new ConfigException($"{document.SourceName}: {WebPortKey}: must be between 0 and 65535", document.SourceName, key: WebPortKey);
        }

        var bind = document.GetString(WebBindKey);
        if (bind is { Length: 0 })
        {
            throw new ConfigException($"{document.SourceName}: {WebBindKey}: must not be empty", document.SourceName, key: WebBindKey);
        }

        var token = document.GetString(WebTokenKey);
        return new WebSettings((int?)port ?? WebSettings.DefaultPort, bind ?? WebSettings.DefaultBind, string.IsNullOrEmpty(token) ? null : token);
    }

    private static string? ReadModel(ConfigDocument document)
    {
        var name = document.GetString(ModelKey);
        if (name is { Length: 0 })
        {
            throw new ConfigException($"{document.SourceName}: {ModelKey}: must not be empty", document.SourceName, key: ModelKey);
        }

        return name;
    }

    private static PathPattern? ReadPattern(ConfigDocument document)
    {
        var text = document.GetString(PatternKey);
        if (text is null)
        {
            return null;
        }

        try
        {
            return PathPattern.Parse(text);
        }
        catch (PatternException e)
        {
            throw new ConfigException($"{document.SourceName}: {PatternKey}: {e.Message}", document.SourceName, key: PatternKey);
        }
    }

    private static int? ReadHamming(ConfigDocument document, string key)
    {
        var value = document.GetInteger(key);
        if (value is < 0 or > 64)
        {
            throw new ConfigException(
                $"{document.SourceName}: {key}: must be between 0 and 64",
                document.SourceName,
                key: key);
        }

        return (int?)value;
    }

    private static List<LibraryEntry> ReadLibraries(ConfigDocument global)
    {
        var entries = new List<LibraryEntry>();
        var index = 0;
        foreach (var table in global.GetTables(LibrariesKey))
        {
            index++;
            var prefix = $"{LibrariesKey}[{index}]";
            var name = table.GetString("name") ?? throw Invalid(global, $"{prefix}.name", "missing");
            var path = table.GetString("path") ?? throw Invalid(global, $"{prefix}.path", "missing");
            if (name.Length == 0)
            {
                throw Invalid(global, $"{prefix}.name", "must not be empty");
            }

            if (!Path.IsPathRooted(path))
            {
                throw Invalid(global, $"{prefix}.path", "must be an absolute path");
            }

            if (entries.Any(e => e.Name == name))
            {
                throw Invalid(global, $"{prefix}.name", $"duplicate library name '{name}'");
            }

            entries.Add(new LibraryEntry(name, Path.TrimEndingDirectorySeparator(path)));
        }

        return entries;
    }

    private static ConfigException Invalid(ConfigDocument document, string key, string problem) =>
        new($"{document.SourceName}: {key}: {problem}", document.SourceName, key: key);
}
