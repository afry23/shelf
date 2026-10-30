using Microsoft.Extensions.Logging;
using Shelf.Index;
using Shelf.Semantic;

namespace Shelf.Cli;

/// <summary>
/// The parts of the process environment that the CLI honors. It is passed in explicitly so that
/// <see cref="ShelfApp"/> never reads the process environment or the static console.
/// </summary>
public sealed class CliEnvironment
{
    private const string LogLevelVariable = "SHELF_LOG_LEVEL";

    private readonly Func<string, string?> _getVariable;
    private readonly string? _workingDirectory;
    private readonly string? _homeDirectory;
    private readonly TextReader? _standardInput;
    private readonly Stream? _mcpInput;
    private readonly Stream? _mcpOutput;
    private readonly IReadOnlyList<IMetadataSource>? _metadataSources;
    private readonly IHttpGetter? _pages;

    private CliEnvironment(
        bool noColor,
        bool stdoutIsTerminal,
        LogLevel minimumLogLevel,
        string? ignoredLogLevel,
        Func<string, string?> getVariable,
        string? workingDirectory,
        string? homeDirectory,
        IFileOpener? fileOpener = null,
        TextReader? standardInput = null,
        ModelCatalog? modelCatalog = null,
        IModelDownloader? modelDownloader = null,
        Func<ModelFolder, IEmbedder>? embedderFactory = null,
        CancellationToken cancellation = default,
        Stream? mcpInput = null,
        Stream? mcpOutput = null,
        IRemoteFetcher? remoteFetcher = null,
        IReadOnlyList<IMetadataSource>? metadataSources = null,
        IHttpGetter? pages = null)
    {
        NoColor = noColor;
        StdoutIsTerminal = stdoutIsTerminal;
        MinimumLogLevel = minimumLogLevel;
        IgnoredLogLevel = ignoredLogLevel;
        _getVariable = getVariable;
        _workingDirectory = workingDirectory;
        _homeDirectory = homeDirectory;
        FileOpener = fileOpener ?? SystemFileOpener.Instance;
        _standardInput = standardInput;
        ModelCatalog = modelCatalog ?? ModelCatalog.Default;
        ModelDownloader = modelDownloader ?? new HttpModelDownloader();
        EmbedderFactory = embedderFactory ?? (folder => E5Embedder.Open(folder));
        Cancellation = cancellation;
        _mcpInput = mcpInput;
        _mcpOutput = mcpOutput;
        RemoteFetcher = remoteFetcher ?? new RcloneFetcher();
        _metadataSources = metadataSources;
        _pages = pages;
    }

    /// <summary>No <c>NO_COLOR</c> and standard output is not a terminal.</summary>
    public static CliEnvironment Empty { get; } = new(noColor: false, stdoutIsTerminal: false, LogLevel.Warning, null, _ => null, null, null);

    /// <summary>True when <c>NO_COLOR</c> is present and non-empty (no-color.org).</summary>
    public bool NoColor { get; }

    public bool StdoutIsTerminal { get; }

    /// <summary>Where interactive commands read the answers from; the process's standard input unless the host set another.</summary>
    public TextReader StandardInput => _standardInput ?? Console.In;

    /// <summary>The models <c>shelf model install</c> knows, with the checksums of their files.</summary>
    public ModelCatalog ModelCatalog { get; }

    /// <summary>How <c>shelf model install</c> fetches files; HTTPS unless the host set another.</summary>
    public IModelDownloader ModelDownloader { get; }

    /// <summary>How <c>shelf import --from</c> fetches a remote folder; the user's rclone unless the host set another.</summary>
    public IRemoteFetcher RemoteFetcher { get; }

    /// <summary>The online catalogs of <c>shelf enrich</c>, in the order they are asked: Open Library, Google Books for books, then Crossref, arXiv, Unpaywall (if it has an e-mail address) for papers, unless the host set others.</summary>
    public IReadOnlyList<IMetadataSource> MetadataSources => _metadataSources ?? DefaultSources();

    /// <summary>How <c>shelf clip</c> loads a page and its images (F-ART-2); HTTPS unless the host set another.</summary>
    public IHttpGetter Pages => _pages ?? new HttpGetter($"shelf/{VersionInfo.Get()} (+https://github.com/afry23/shelf)");

    private static IMetadataSource[] DefaultSources()
    {
        var http = new HttpGetter($"shelf/{VersionInfo.Get()} (+https://github.com/afry23/shelf)");
        return [new OpenLibrarySource(http), new GoogleBooksSource(http), new CrossrefSource(http), new ArxivSource(http), new UnpaywallSource(http, null)];
    }

    /// <summary>Opens the embedding model of a folder; ONNX Runtime unless the host set another.</summary>
    public Func<ModelFolder, IEmbedder> EmbedderFactory { get; }

    /// <summary>Set when the user interrupts a long run (Ctrl-C); long commands stop at a safe point.</summary>
    public CancellationToken Cancellation { get; }

    /// <summary>Where <c>shelf mcp</c> reads requests from: the process's standard input unless the host set another stream.</summary>
    public Stream McpInput => _mcpInput ?? Console.OpenStandardInput();

    /// <summary>Where <c>shelf mcp</c> writes responses: the process's standard output unless the host set another stream.</summary>
    public Stream McpOutput => _mcpOutput ?? Console.OpenStandardOutput();

    /// <summary>What <c>shelf open</c> uses to start the application for a file; the system's by default.</summary>
    public IFileOpener FileOpener { get; }

    /// <summary>
    /// Minimum level of log entries written to standard error. Set with the SHELF_LOG_LEVEL variable
    /// (a <see cref="LogLevel"/> name, case-insensitive) or programmatically with <see cref="WithMinimumLogLevel"/>.
    /// </summary>
    public LogLevel MinimumLogLevel { get; }

    /// <summary>The SHELF_LOG_LEVEL value that is not a log level name, or null. The CLI reports it and keeps the default.</summary>
    public string? IgnoredLogLevel { get; }

    /// <summary>Colored output is allowed only on a terminal and without <c>NO_COLOR</c>.</summary>
    public bool UseColor => StdoutIsTerminal && !NoColor;

    public static CliEnvironment FromVariables(Func<string, string?> getVariable, bool stdoutIsTerminal)
    {
        ArgumentNullException.ThrowIfNull(getVariable);
        var noColor = !string.IsNullOrEmpty(getVariable("NO_COLOR"));
        var level = LogLevel.Warning;
        string? ignored = null;
        var value = getVariable(LogLevelVariable);
        if (!string.IsNullOrEmpty(value))
        {
            // Names only: Enum.TryParse would also accept numbers like "3".
            if (Array.Exists(Enum.GetNames<LogLevel>(), n => n.Equals(value, StringComparison.OrdinalIgnoreCase)))
            {
                level = Enum.Parse<LogLevel>(value, ignoreCase: true);
            }
            else
            {
                ignored = value;
            }
        }

        return new CliEnvironment(noColor, stdoutIsTerminal, level, ignored, getVariable, null, null);
    }

    /// <summary>The value of a process environment variable, or null.</summary>
    public string? GetVariable(string name) => _getVariable(name);

    /// <summary>The directory commands resolve relative paths against. Set by the host; a command that needs it fails if it is missing.</summary>
    public string WorkingDirectory =>
        _workingDirectory ?? throw new InvalidOperationException("The working directory is not known.");

    /// <summary>The home directory, used to find the global config file.</summary>
    public string HomeDirectory =>
        _homeDirectory ?? throw new InvalidOperationException("The home directory is not known.");

    public CliEnvironment WithDirectories(string workingDirectory, string homeDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(workingDirectory);
        ArgumentException.ThrowIfNullOrEmpty(homeDirectory);
        return new CliEnvironment(NoColor, StdoutIsTerminal, MinimumLogLevel, IgnoredLogLevel, _getVariable, workingDirectory, homeDirectory, FileOpener, _standardInput, ModelCatalog, ModelDownloader, EmbedderFactory, Cancellation, _mcpInput, _mcpOutput, RemoteFetcher, _metadataSources, _pages);
    }

    public CliEnvironment WithMinimumLogLevel(LogLevel level)
    {
        ArgumentOutOfRangeException.ThrowIfNegative((int)level);
        if (level > LogLevel.None)
        {
            throw new ArgumentOutOfRangeException(nameof(level), level, "Not a defined log level.");
        }

        return new CliEnvironment(NoColor, StdoutIsTerminal, level, IgnoredLogLevel, _getVariable, _workingDirectory, _homeDirectory, FileOpener, _standardInput, ModelCatalog, ModelDownloader, EmbedderFactory, Cancellation, _mcpInput, _mcpOutput, RemoteFetcher, _metadataSources, _pages);
    }

    public CliEnvironment WithFileOpener(IFileOpener fileOpener)
    {
        ArgumentNullException.ThrowIfNull(fileOpener);
        return new CliEnvironment(NoColor, StdoutIsTerminal, MinimumLogLevel, IgnoredLogLevel, _getVariable, _workingDirectory, _homeDirectory, fileOpener, _standardInput, ModelCatalog, ModelDownloader, EmbedderFactory, Cancellation, _mcpInput, _mcpOutput, RemoteFetcher, _metadataSources, _pages);
    }

    public CliEnvironment WithStandardInput(TextReader standardInput)
    {
        ArgumentNullException.ThrowIfNull(standardInput);
        return new CliEnvironment(NoColor, StdoutIsTerminal, MinimumLogLevel, IgnoredLogLevel, _getVariable, _workingDirectory, _homeDirectory, FileOpener, standardInput, ModelCatalog, ModelDownloader, EmbedderFactory, Cancellation, _mcpInput, _mcpOutput, RemoteFetcher, _metadataSources, _pages);
    }

    public CliEnvironment WithModels(ModelCatalog catalog, IModelDownloader downloader)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(downloader);
        return new CliEnvironment(NoColor, StdoutIsTerminal, MinimumLogLevel, IgnoredLogLevel, _getVariable, _workingDirectory, _homeDirectory, FileOpener, _standardInput, catalog, downloader, EmbedderFactory, Cancellation, _mcpInput, _mcpOutput, RemoteFetcher, _metadataSources, _pages);
    }

    public CliEnvironment WithRemoteFetcher(IRemoteFetcher fetcher)
    {
        ArgumentNullException.ThrowIfNull(fetcher);
        return new CliEnvironment(NoColor, StdoutIsTerminal, MinimumLogLevel, IgnoredLogLevel, _getVariable, _workingDirectory, _homeDirectory, FileOpener, _standardInput, ModelCatalog, ModelDownloader, EmbedderFactory, Cancellation, _mcpInput, _mcpOutput, fetcher, _metadataSources, _pages);
    }

    public CliEnvironment WithMetadataSources(IReadOnlyList<IMetadataSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        return new CliEnvironment(NoColor, StdoutIsTerminal, MinimumLogLevel, IgnoredLogLevel, _getVariable, _workingDirectory, _homeDirectory, FileOpener, _standardInput, ModelCatalog, ModelDownloader, EmbedderFactory, Cancellation, _mcpInput, _mcpOutput, RemoteFetcher, sources, _pages);
    }

    public CliEnvironment WithPages(IHttpGetter pages)
    {
        ArgumentNullException.ThrowIfNull(pages);
        return new CliEnvironment(NoColor, StdoutIsTerminal, MinimumLogLevel, IgnoredLogLevel, _getVariable, _workingDirectory, _homeDirectory, FileOpener, _standardInput, ModelCatalog, ModelDownloader, EmbedderFactory, Cancellation, _mcpInput, _mcpOutput, RemoteFetcher, _metadataSources, pages);
    }

    public CliEnvironment WithEmbedder(Func<ModelFolder, IEmbedder> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        return new CliEnvironment(NoColor, StdoutIsTerminal, MinimumLogLevel, IgnoredLogLevel, _getVariable, _workingDirectory, _homeDirectory, FileOpener, _standardInput, ModelCatalog, ModelDownloader, factory, Cancellation, _mcpInput, _mcpOutput, RemoteFetcher, _metadataSources, _pages);
    }

    public CliEnvironment WithCancellation(CancellationToken cancellation) =>
        new(NoColor, StdoutIsTerminal, MinimumLogLevel, IgnoredLogLevel, _getVariable, _workingDirectory, _homeDirectory, FileOpener, _standardInput, ModelCatalog, ModelDownloader, EmbedderFactory, cancellation, _mcpInput, _mcpOutput, RemoteFetcher, _metadataSources, _pages);

    public CliEnvironment WithMcpStreams(Stream input, Stream output)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        return new CliEnvironment(NoColor, StdoutIsTerminal, MinimumLogLevel, IgnoredLogLevel, _getVariable, _workingDirectory, _homeDirectory, FileOpener, _standardInput, ModelCatalog, ModelDownloader, EmbedderFactory, Cancellation, input, output, RemoteFetcher, _metadataSources, _pages);
    }
}
