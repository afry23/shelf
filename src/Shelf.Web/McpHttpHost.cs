using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Routing;
using Shelf.Mcp;

namespace Shelf.Web;

/// <summary>
/// <c>shelf mcp --http</c> (PRD 7.2): the MCP server of a library over Streamable HTTP at <c>/mcp</c>. The tools are those of
/// the stdio server, including <c>mcp.read_only</c>. Every request is answered on its own (stateless mode), so there are no
/// sessions to keep or to expire. The rules of the web server hold: loopback unless the caller binds elsewhere, and then a
/// bearer token is required; a server on a loopback address answers to loopback host names only.
/// </summary>
public static class McpHttpHost
{
    /// <summary>The path of the endpoint.</summary>
    public const string Path = "/mcp";

    /// <summary>Runs until <paramref name="cancellation"/> is cancelled.</summary>
    /// <exception cref="WebHostException">The port is in use, or the server could not start.</exception>
    public static async Task RunAsync(
        WebOptions options,
        ILoggerProvider logging,
        LogLevel minimumLevel,
        TextWriter stderr,
        CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logging);
        ArgumentNullException.ThrowIfNull(stderr);

        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { ContentRootPath = AppContext.BaseDirectory });
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(logging);
        builder.Logging.SetMinimumLevel(minimumLevel);
        var framework = minimumLevel > LogLevel.Debug ? LogLevel.Warning : minimumLevel;
        builder.Logging.AddFilter("Microsoft", framework);
        builder.Logging.AddFilter("System", framework);

        // The request log prints the query string; a client that puts ?token= there must not get the token into a log.
        builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);

        builder.Services.AddSingleton<IHostLifetime, CancellationLifetime>();
        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.AddServerHeader = false;
            kestrel.Listen(options.Address, options.Port);
        });

        using var library = LibraryMcpServer.Open(options.Root, options.Settings, options.Version, options.Models, options.EmbedderFactory);
        builder.Services
            .AddMcpServer()
            .WithHttpTransport(http =>
            {
                http.Stateless = true;
                http.ConfigureSessionOptions = (_, server, _) =>
                {
                    server.ServerInfo = library.Options.ServerInfo;
                    server.ServerInstructions = library.Options.ServerInstructions;
                    server.ToolCollection = library.Options.ToolCollection;
                    server.ResourceCollection = library.Options.ResourceCollection;
                    return Task.CompletedTask;
                };
            });

        var app = builder.Build();
        Configure(app, options);

        app.Lifetime.ApplicationStarted.Register(() =>
        {
            foreach (var url in app.Urls)
            {
                stderr.WriteLine($"Listening on {url}{Path}");
            }
        });

        try
        {
            await app.RunAsync(cancellation).ConfigureAwait(false);
        }
        catch (IOException e) when (e.InnerException is SocketException { SocketErrorCode: SocketError.AddressAlreadyInUse })
        {
            throw new WebHostException($"port {options.Port} is already in use");
        }
    }

    private static void Configure(WebApplication app, WebOptions options)
    {
        if (IPAddress.IsLoopback(options.Address))
        {
            app.Use(async (context, next) =>
            {
                if (!HostCheck.IsLoopbackHost(context.Request.Host.Value))
                {
                    await Problems.WriteAsync(context, StatusCodes.Status421MisdirectedRequest, "this server answers to localhost only").ConfigureAwait(false);
                    return;
                }

                await next(context).ConfigureAwait(false);
            });
        }

        if (!string.IsNullOrEmpty(options.Token))
        {
            app.Use(async (context, next) =>
            {
                if (!AccessToken.Matches(options.Token, AccessToken.FromAuthorization(context.Request.Headers.Authorization.ToString())))
                {
                    context.Response.Headers.WWWAuthenticate = "Bearer";
                    await Problems.WriteAsync(context, StatusCodes.Status401Unauthorized, "a valid access token is required").ConfigureAwait(false);
                    return;
                }

                await next(context).ConfigureAwait(false);
            });
        }

        app.UseStatusCodePages(context => Problems.WriteAsync(context.HttpContext, context.HttpContext.Response.StatusCode));
        app.UseRouting();
        app.MapMcp(Path);
    }

    /// <summary>The host stops when the token it is run with is cancelled; it does not listen for signals itself.</summary>
    private sealed class CancellationLifetime : IHostLifetime
    {
        public Task WaitForStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
