using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.FileProviders;
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

    /// <summary>
    /// The reader opens books that other people made, and foliate-js renders them from blob: URLs of this origin, where a frame
    /// cannot be sandboxed: only a policy that allows no script but the program's own keeps a book from running code (its README
    /// calls the policy imperative). blob: is no script source, so a script inside a blob frame is blocked. 'unsafe-inline' is for
    /// styles only (the libraries write style elements); remote sources are not allowed at all (PRD 9.4: no network access).
    /// </summary>
    internal const string ContentSecurityPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; "
        + "font-src 'self' data: blob:; media-src 'self' blob:; connect-src 'self'; worker-src 'self' blob:; "
        + "frame-src 'self' blob:; object-src 'none'; base-uri 'none'; form-action 'self'; frame-ancestors 'self'";

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

        using var library = new LibraryHandle(options);
        builder.Services.AddSingleton(library);

        var app = builder.Build();
        Configure(app, options, stderr);

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

    private static void Configure(WebApplication app, WebOptions options, TextWriter stderr)
    {
        // On every answer, whatever it is.
        app.Use((context, next) =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "SAMEORIGIN";
            headers["Referrer-Policy"] = "same-origin";
            headers.ContentSecurityPolicy = ContentSecurityPolicy;
            return next(context);
        });

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

        var ui = EmbeddedUi();
        if (ui is null)
        {
            stderr.WriteLine("warning: the UI is not embedded in this build; the pages answer with a problem document");
        }
        else
        {
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = ui,

                // The assets of PDF.js have extensions that no table knows (.bcmap, .pfb); they come from our own embedded folder.
                ServeUnknownFileTypes = true,
                DefaultContentType = "application/octet-stream",
                OnPrepareResponse = prepared =>
                {
                    // Vite puts a hash of the content into the names under /assets/: such a file never changes.
                    if (prepared.Context.Request.Path.StartsWithSegments("/assets", StringComparison.Ordinal))
                    {
                        prepared.Context.Response.Headers.CacheControl = "public,max-age=31536000,immutable";
                    }
                },
            });
        }

        if (ui is not null)
        {
            // The framework's fallback only takes addresses without a dot (it adds the constraint "nonfile" to any pattern). The
            // address of the reader ends in the name of a book (/read/01J9...~kp.epub), and reloading it must give the page, so a
            // browser's request for a page is sent to the root, where the fallback answers. A script or a style asks for anything
            // but a page and gets its 404.
            app.Use((context, next) =>
            {
                var request = context.Request;
                if ((HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method))
                    && !request.Path.StartsWithSegments("/api", StringComparison.Ordinal)
                    && Path.HasExtension(request.Path.Value)
                    && request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase))
                {
                    request.Path = "/";
                }

                return next(context);
            });
        }

        // The routing is placed here, by hand, so that the middleware above can still change the path before an endpoint is chosen
        // (the framework adds it at the very start of the pipeline otherwise).
        app.UseRouting();

        var api = app.MapGroup(ApiPrefix);
        api.MapGet("/status", Ok<StatusResponse> () =>
            {
                using var connection = IndexDatabase.OpenRead(IndexDatabase.PathFor(options.Root));
                var statistics = LibraryStatistics.Load(connection);
                return TypedResults.Ok(new StatusResponse(options.Version, statistics.Works, statistics.Files, options.Settings.Web.PageSize));
            })
            .WithName("GetStatus")
            .Produces<StatusResponse>(StatusCodes.Status200OK);

        Api.Map(api);

        // The page for every address without a file name (the browser runs its own routes), and a 404 for the rest. Only GET and
        // HEAD: a fallback for every method would be a valid candidate for a POST to an endpoint of the API and turn its 405 into
        // a 404. The framework's MapFallbackToFile would answer /api/v1/nothing with the page.
        app.MapFallback("{**path}", (HttpContext context, string? path) => Fallback(context, ui, path))
            .WithMetadata(new HttpMethodMetadata(["GET", "HEAD"]))
            .ExcludeFromDescription();

        app.MapOpenApi(ApiPrefix + "/openapi.json");
    }

    private static IResult Fallback(HttpContext context, IFileProvider? ui, string? path)
    {
        path ??= string.Empty;
        var isApi = path == "api" || path.StartsWith("api/", StringComparison.Ordinal);
        if (isApi || ui is null)
        {
            return new ProblemResult(StatusCodes.Status404NotFound, isApi ? "no such endpoint" : "no such file");
        }

        var page = ui.GetFileInfo("index.html");
        if (!page.Exists)
        {
            return new ProblemResult(StatusCodes.Status404NotFound, "no such file");
        }

        // The page is checked on every load, so that a new build is seen at once; its ETag makes the check cheap.
        context.Response.Headers.CacheControl = "no-cache";
        var tag = new Microsoft.Net.Http.Headers.EntityTagHeaderValue($"\"{page.Length.ToString("x", System.Globalization.CultureInfo.InvariantCulture)}-{page.LastModified.UtcTicks.ToString("x", System.Globalization.CultureInfo.InvariantCulture)}\"");
        return Results.Stream(page.CreateReadStream(), "text/html; charset=utf-8", lastModified: page.LastModified, entityTag: tag);
    }

    /// <summary>The UI that the build embedded, or null when this build has none (<c>SkipUiBuild</c>).</summary>
    private static ManifestEmbeddedFileProvider? EmbeddedUi()
    {
        try
        {
            var provider = new ManifestEmbeddedFileProvider(typeof(WebHost).Assembly, "ui");
            return provider.GetFileInfo("index.html").Exists ? provider : null;
        }
        catch (InvalidOperationException)
        {
            // No manifest in the assembly: built without the UI.
            return null;
        }
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
