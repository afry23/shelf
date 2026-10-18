using System.CommandLine;
using Shelf.Cli.Logging;
using Shelf.Core.Configuration;
using Shelf.Web;

namespace Shelf.Cli.Commands;

/// <summary>
/// <c>shelf serve</c> (F-WEB-1, F-WEB-2): the web interface and API. It listens on the loopback address unless
/// <c>--bind</c> says otherwise, and then it needs a token. The address is announced on standard error; standard output
/// stays empty.
/// </summary>
internal static class ServeCommand
{
    public static Command Create(Option<bool> json, TextWriter stdout, TextWriter stderr, CliEnvironment environment)
    {
        var library = LibraryContext.CreateOption();
        var port = new Option<int?>("--port") { Description = $"Port to listen on, 0 for any free port (default: web.port, else {WebSettings.DefaultPort})" };
        var bind = new Option<string?>("--bind") { Description = $"IP address to listen on; anything but a loopback address needs a token (default: web.bind, else {WebSettings.DefaultBind})" };
        var token = new Option<string?>("--token") { Description = "Access token for the API (default: web.token); required for an address that is not loopback" };
        var command = new Command("serve", "Run the web interface and API");
        command.Options.Add(library);
        command.Options.Add(port);
        command.Options.Add(bind);
        command.Options.Add(token);
        command.SetAction(parseResult =>
        {
            var context = LibraryContext.Open(parseResult.GetValue(library), environment, stderr);
            var web = context.Settings.Web;
            var chosenPort = parseResult.GetValue(port) ?? web.Port;
            if (chosenPort is < 0 or > 65535)
            {
                stderr.WriteLine("error: --port must be between 0 and 65535");
                return ExitCodes.Usage;
            }

            // An empty token counts as none, on the command line as well as in the config.
            var chosenToken = parseResult.GetResult(token) is null ? web.Token : parseResult.GetValue(token);
            if (string.IsNullOrEmpty(chosenToken))
            {
                chosenToken = null;
            }

            if (!BindRules.TryValidate(parseResult.GetValue(bind) ?? web.Bind, chosenToken, out var address, out var error))
            {
                stderr.WriteLine($"error: {error}");
                return ExitCodes.Usage;
            }

            using var provider = new StderrLoggerProvider(stderr);
            try
            {
                WebHost
                    .RunAsync(new WebOptions(context.Root, context.Settings, VersionInfo.Get(), address, chosenPort, chosenToken, environment.ModelCatalog, environment.EmbedderFactory), provider, environment.MinimumLogLevel, stderr, environment.Cancellation)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (OperationCanceledException)
            {
                // Ctrl-C: the user stopped the server.
            }

            return ExitCodes.Success;
        });
        return command;
    }
}
