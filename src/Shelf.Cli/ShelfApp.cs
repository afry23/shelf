using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Shelf.Cli.Commands;
using Shelf.Cli.Logging;

namespace Shelf.Cli;

/// <summary>
/// Entry point of the CLI (ADR 0002: System.CommandLine). Exit codes and stream rules follow PRD 7.1.
/// Never touches the static console or the process environment; everything comes in through the arguments.
/// </summary>
public static class ShelfApp
{
    private const string AppName = "shelf";
    private const string Usage = "Usage: shelf <command> [options]";

    public static int Run(string[] args, TextWriter stdout, TextWriter stderr) =>
        Run(args, stdout, stderr, CliEnvironment.Empty);

    public static int Run(string[] args, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdout);
        ArgumentNullException.ThrowIfNull(stderr);
        ArgumentNullException.ThrowIfNull(environment);

        if (environment.IgnoredLogLevel is { } ignored)
        {
            stderr.WriteLine($"warning: ignoring invalid SHELF_LOG_LEVEL '{ignored}'");
        }

        using var provider = new StderrLoggerProvider(stderr);
        using var factory = new LoggerFactory([provider], new LoggerFilterOptions { MinLevel = environment.MinimumLogLevel });
        var logger = factory.CreateLogger("Shelf.Cli.ShelfApp");
        ShelfAppLog.Starting(logger, VersionInfo.Get());
        var code = RunCore(args, stdout, stderr, logger, environment);
        ShelfAppLog.Exiting(logger, code);
        return code;
    }

    private static int RunCore(string[] args, TextWriter stdout, TextWriter stderr, ILogger logger, CliEnvironment environment)
    {
        // A plain Command is the root (not RootCommand): RootCommand derives its name from the process
        // path, which is the test host in specs (ADR 0002).
        var json = GlobalOptions.CreateJson();
        var root = new Command(AppName, "Manage your e-book library");
        root.Options.Add(json);
        root.Options.Add(new HelpOption());
        root.Options.Add(new Option<bool>("--version")
        {
            Description = "Show version information",
            Action = new VersionAction(json, stdout),
        });

        root.Subcommands.Add(InitCommand.Create(json, stdout, stderr, environment));
        root.Subcommands.Add(ReindexCommand.Create(json, stdout, stderr, environment));
        root.Subcommands.Add(ImportCommand.Create(json, stdout, stderr, environment));
        root.Subcommands.Add(ScanCommand.Create(json, stdout, stderr, environment));
        root.Subcommands.Add(SearchCommand.Create(json, stdout, stderr, environment));
        root.Subcommands.Add(ShowCommand.Create(json, stdout, stderr, environment));
        root.Subcommands.Add(EditCommand.Create(json, stdout, stderr, environment));
        root.Subcommands.Add(CiteKeysCommand.Create(json, stdout, stderr, environment));
        root.Subcommands.Add(ExportCommand.Create(json, stdout, stderr, environment));
        root.Subcommands.Add(ImportBibCommand.Create(json, stdout, stderr, environment));
        root.Subcommands.Add(FetchCommand.Create(json, stdout, stderr, environment));
        root.Subcommands.Add(EnrichCommand.Create(json, stdout, stderr, environment));
        root.Subcommands.Add(MergeCommand.Create(json, stdout, stderr, environment));
        root.Subcommands.Add(SplitCommand.Create(json, stdout, stderr, environment));
        root.Subcommands.Add(DeleteCommand.Create(json, stdout, stderr, environment));
        root.Subcommands.Add(AddCommand.Create(json, stdout, stderr, environment));
        root.Subcommands.Add(CreateCommand.Create(json, stdout, stderr, environment));
        root.Subcommands.Add(MoveCommand.Create(json, stdout, stderr, environment));
        root.Subcommands.Add(ReviewCommand.Create(json, stdout, stderr, environment));
        root.Subcommands.Add(OpenCommand.Create(json, stdout, stderr, environment));
        root.Subcommands.Add(StatsCommand.Create(json, stdout, stderr, environment));
        root.Subcommands.Add(OrganizeCommand.Create(json, stdout, stderr, environment));
        root.Subcommands.Add(UndoCommand.Create(json, stdout, stderr, environment));
        root.Subcommands.Add(TagCommand.Create(json, stdout, stderr, environment));
        root.Subcommands.Add(NotesCommand.Create(json, stdout, stderr, environment));
        root.Subcommands.Add(CiteCommand.Create(json, stdout, stderr, environment));
        root.Subcommands.Add(DupesCommand.Create(json, stdout, stderr, environment));
        root.Subcommands.Add(TrashCommand.Create(json, stdout, stderr, environment));
        root.Subcommands.Add(ModelCommand.Create(json, stdout, stderr, environment));
        root.Subcommands.Add(SemanticCommand.Create(json, stdout, stderr, environment));
        root.Subcommands.Add(SimilarCommand.Create(json, stdout, stderr, environment));
        root.Subcommands.Add(McpCommand.Create(json, stdout, stderr, environment));
        root.Subcommands.Add(CoversCommand.Create(json, stdout, stderr, environment));
        root.Subcommands.Add(ServeCommand.Create(json, stdout, stderr, environment));
        root.Subcommands.Add(CompletionCommand.Create(root, stdout));

        // Without a command there is nothing to run: that is a usage error, not success.
        root.SetAction(_ =>
        {
            stderr.WriteLine(Usage);
            return ExitCodes.Usage;
        });

        var parseResult = root.Parse(args);
        ShelfAppLog.Parsed(logger, parseResult.Tokens.Count, parseResult.Errors.Count);
        if (parseResult.Errors.Count > 0)
        {
            // Our own text for unmatched tokens; otherwise the framework's (missing argument, invalid value).
            var message = ParseErrorMessages.Describe(parseResult.UnmatchedTokens, atRoot: parseResult.CommandResult.Command == root)
                ?? parseResult.Errors[0].Message;
            stderr.WriteLine(message);

            stderr.WriteLine(Usage);
            return ExitCodes.Usage;
        }

        try
        {
            return parseResult.Invoke(new InvocationConfiguration
            {
                Output = stdout,
                Error = stderr,
                EnableDefaultExceptionHandler = false,
            });
        }
        catch (Exception exception)
        {
            stderr.WriteLine($"error: {exception.Message}");
            return ExitCodes.Error;
        }
    }

    private sealed class VersionAction(Option<bool> json, TextWriter stdout) : SynchronousCommandLineAction
    {
        public override int Invoke(ParseResult parseResult)
        {
            var version = VersionInfo.Get();
            if (parseResult.GetValue(json))
            {
                using var buffer = new MemoryStream();
                using (var writer = new Utf8JsonWriter(buffer))
                {
                    writer.WriteStartObject();
                    writer.WriteString("version", version);
                    writer.WriteEndObject();
                }

                stdout.WriteLine(System.Text.Encoding.UTF8.GetString(buffer.ToArray()));
            }
            else
            {
                stdout.WriteLine($"{AppName} {version}");
            }

            return ExitCodes.Success;
        }
    }
}
