using System.CommandLine;
using Microsoft.Extensions.Logging;
using Shelf.Cli.Logging;
using Shelf.Core.Configuration;
using Shelf.Mcp;
using Shelf.Web;

namespace Shelf.Cli.Commands;

/// <summary>
/// <c>shelf mcp</c> (PRD 7.2): the MCP server on standard input and output, for assistants such as Claude Desktop.
/// Standard output carries the protocol only; messages and log lines go to standard error. With <c>--http</c> the server
/// speaks Streamable HTTP at <c>/mcp</c> instead, on the loopback address unless <c>--bind</c> says otherwise (and then a
/// token is required, as for <c>serve</c>).
/// </summary>
internal static class McpCommand
{
    public static Command Create(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var library = LibraryContext.CreateOption();
        var command = new Command("mcp", "Run the MCP server on standard input and output, for AI assistants");
        var http = new Option<bool>("--http") { Description = "Serve Streamable HTTP at /mcp instead of standard input and output" };
        var port = new Option<int?>("--port") { Description = $"With --http: port to listen on, 0 for any free port (default: {McpSettings.DefaultHttpPort})" };
        var bind = new Option<string?>("--bind") { Description = $"With --http: IP address to listen on; anything but a loopback address needs a token (default: web.bind, else {WebSettings.DefaultBind})" };
        var token = new Option<string?>("--token") { Description = "With --http: access token, sent as a bearer token (default: web.token); required for an address that is not loopback" };
        command.Options.Add(library);
        command.Options.Add(http);
        command.Options.Add(port);
        command.Options.Add(bind);
        command.Options.Add(token);
        command.SetAction(parseResult =>
        {
            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            if (!parseResult.GetValue(http))
            {
                var misplaced = new Option[] { port, bind, token }.Where(o => parseResult.GetResult(o) is not null).Select(o => o.Name).ToArray();
                if (misplaced.Length > 0)
                {
                    stderr.WriteLine($"error: {string.Join(", ", misplaced)} only apply with --http");
                    return ExitCodes.Usage;
                }
            }

            using var provider = new StderrLoggerProvider(stderr);
            using var factory = new LoggerFactory([provider], new LoggerFilterOptions { MinLevel = environment.MinimumLogLevel });
            if (parseResult.GetValue(http))
            {
                return RunHttp(parseResult.GetValue(port), parseResult.GetValue(bind), parseResult.GetResult(token) is null ? context.Settings.Web.Token : parseResult.GetValue(token), context, provider, stderr, environment);
            }

            try
            {
                LibraryMcpServer
                    .RunAsync(context.Root, context.Settings, VersionInfo.Get(), environment.ModelCatalog, environment.EmbedderFactory, environment.McpInput, environment.McpOutput, factory, environment.Cancellation)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (OperationCanceledException)
            {
                // Ctrl-C: the client has gone or the user stopped the server.
            }

            return ExitCodes.Success;
        });
        return command;
    }

    private static int RunHttp(int? port, string? bind, string? token, LibraryContext context, StderrLoggerProvider provider, TextWriter stderr, CliEnvironment environment)
    {
        var chosenPort = port ?? McpSettings.DefaultHttpPort;
        if (chosenPort is < 0 or > 65535)
        {
            stderr.WriteLine("error: --port must be between 0 and 65535");
            return ExitCodes.Usage;
        }

        // An empty token counts as none, on the command line as well as in the config.
        if (string.IsNullOrEmpty(token))
        {
            token = null;
        }

        if (!BindRules.TryValidate(bind ?? context.Settings.Web.Bind, token, out var address, out var error))
        {
            stderr.WriteLine($"error: {error}");
            return ExitCodes.Usage;
        }

        try
        {
            McpHttpHost
                .RunAsync(new WebOptions(context.Root, context.Settings, VersionInfo.Get(), address, chosenPort, token, environment.ModelCatalog, environment.EmbedderFactory), provider, environment.MinimumLogLevel, stderr, environment.Cancellation)
                .GetAwaiter()
                .GetResult();
        }
        catch (OperationCanceledException)
        {
            // Ctrl-C: the user stopped the server.
        }

        return ExitCodes.Success;
    }
}
