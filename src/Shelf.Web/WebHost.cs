using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Http.HttpResults;
using Shelf.Index;

namespace Shelf.Web;

/// <summary>
/// The web server of a library (PRD 7.3, ADR 0007): Kestrel on the slim builder, Minimal APIs under <c>/api/v1</c> with
/// a source-generated JSON context, no reflection. Standard output is never used; the address is announced on
/// <c>stderr</c> and every log line goes to the logger provider.
/// </summary>
public static class WebHost
{
    private const string ApiPrefix = "/api/v1";

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

        // The content root is the folder of the program, not the working directory: an appsettings.json lying in the
        // folder the user happens to be in must not change the server.
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { ContentRootPath = AppContext.BaseDirectory });
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(logging);
        builder.Logging.SetMinimumLevel(minimumLevel);

        // Framework libraries are noisy at Information. The request log is held at Warning whatever the level: it
        // prints the query string, and a link with ?token= must not put the token into a log.
        var framework = minimumLevel > LogLevel.Debug ? LogLevel.Warning : minimumLevel;
        builder.Logging.AddFilter("Microsoft", framework);
        builder.Logging.AddFilter("System", framework);
        builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);

        // The process handles Ctrl-C itself (Program.cs) and hands the token in; a second lifetime would register
        // signal handlers per server.
        builder.Services.AddSingleton<IHostLifetime, CancellationLifetime>();
        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.AddServerHeader = false;
            kestrel.Listen(options.Address, options.Port);
        });
        builder.Services.ConfigureHttpJsonOptions(json => json.SerializerOptions.TypeInfoResolverChain.Insert(0, WebJsonContext.Default));
        builder.Services.AddOpenApi();

        var app = builder.Build();
        Configure(app, options);

        app.Lifetime.ApplicationStarted.Register(() =>
        {
            foreach (var url in app.Urls)
            {
                stderr.WriteLine($"Listening on {url}");
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
            app.Use((context, next) => CheckToken(context, next, options.Token));
        }

        // A status without a body (404, 405, an unreadable request body) gets the problem document too.
        app.UseStatusCodePages(context => Problems.WriteAsync(context.HttpContext, context.HttpContext.Response.StatusCode));

        var api = app.MapGroup(ApiPrefix);
        api.MapGet("/status", Ok<StatusResponse> () =>
            {
                using var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(options.Root));
                var statistics = LibraryStatistics.Load(connection);
                return TypedResults.Ok(new StatusResponse(options.Version, statistics.Works, statistics.Files));
            })
            .WithName("GetStatus")
            .Produces<StatusResponse>(StatusCodes.Status200OK);

        app.MapOpenApi(ApiPrefix + "/openapi.json");
    }

    /// <summary>
    /// <c>?token=</c> on any address sets the cookie and leads to the same address without it (so the token does not stay
    /// in the address bar or the history); <c>/api/v1</c> needs the bearer header or that cookie.
    /// </summary>
    private static async Task CheckToken(HttpContext context, RequestDelegate next, string token)
    {
        var request = context.Request;
        if (request.Query.TryGetValue(AccessToken.QueryName, out var offered))
        {
            if (!AccessToken.Matches(token, offered.ToString()))
            {
                await Refuse(context).ConfigureAwait(false);
                return;
            }

            context.Response.Cookies.Append(AccessToken.CookieName, token, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                Path = "/",
            });
            context.Response.StatusCode = StatusCodes.Status303SeeOther;
            context.Response.Headers.Location = request.Path.ToUriComponent() + WithoutToken(request.QueryString.Value);
            return;
        }

        if (request.Path.StartsWithSegments(ApiPrefix, StringComparison.Ordinal)
            && !AccessToken.Matches(token, AccessToken.FromAuthorization(request.Headers.Authorization.ToString()))
            && !AccessToken.Matches(token, request.Cookies[AccessToken.CookieName]))
        {
            await Refuse(context).ConfigureAwait(false);
            return;
        }

        await next(context).ConfigureAwait(false);
    }

    private static Task Refuse(HttpContext context)
    {
        context.Response.Headers.WWWAuthenticate = "Bearer";
        return Problems.WriteAsync(context, StatusCodes.Status401Unauthorized, "a valid access token is required");
    }

    /// <summary>The query without its <c>token</c> parameters, as it was written (no decoding, the order stays); empty or starting with <c>?</c>.</summary>
    internal static string WithoutToken(string? query)
    {
        if (string.IsNullOrEmpty(query))
        {
            return string.Empty;
        }

        var kept = query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(part => !(part.Equals(AccessToken.QueryName, StringComparison.Ordinal)
                || part.StartsWith(AccessToken.QueryName + "=", StringComparison.Ordinal)))
            .ToArray();
        return kept.Length == 0 ? string.Empty : "?" + string.Join('&', kept);
    }

    /// <summary>The host stops when the token it is run with is cancelled; it does not listen for signals itself.</summary>
    private sealed class CancellationLifetime : IHostLifetime
    {
        public Task WaitForStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
