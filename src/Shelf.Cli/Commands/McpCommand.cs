using System.CommandLine;
using Microsoft.Extensions.Logging;
using Shelf.Cli.Logging;
using Shelf.Mcp;

namespace Shelf.Cli.Commands;

/// <summary>
/// <c>shelf mcp</c> (PRD 7.2): the MCP server on standard input and output, for assistants such as Claude Desktop.
/// Standard output carries the protocol only; messages and log lines go to standard error.
/// </summary>
internal static class McpCommand
{
    public static Command Create(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var library = LibraryContext.CreateOption();
        var command = new Command("mcp", "Run the MCP server on standard input and output, for AI assistants");
        command.Options.Add(library);
        command.SetAction(parseResult =>
        {
            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            using var provider = new StderrLoggerProvider(stderr);
            using var factory = new LoggerFactory([provider], new LoggerFilterOptions { MinLevel = environment.MinimumLogLevel });
            try
            {
                LibraryMcpServer
                    .RunAsync(context.Root, context.Settings, VersionInfo.Get(), environment.McpInput, environment.McpOutput, factory, environment.Cancellation)
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
}
