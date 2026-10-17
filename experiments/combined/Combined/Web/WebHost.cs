using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;

namespace Proto.Web;

/// <summary>The web server of experiments/web (ADR 0007), unchanged apart from being a method.</summary>
internal static class WebHost
{
    public static async Task<int> RunAsync(string bind, int port, string? token, string? file)
    {
        if (!Auth.TryValidateBind(bind, token, out var address, out var bindError))
        {
            Console.Error.WriteLine($"error: {bindError}");
            return 2;
        }

        var builder = WebApplication.CreateSlimBuilder(args: []);
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new StderrLoggerProvider());
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.WebHost.ConfigureKestrel(o => o.Listen(address, port));
        builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.TypeInfoResolverChain.Insert(0, WebJsonContext.Default));
        builder.Services.AddProblemDetails();
        builder.Services.AddOpenApi();

        var app = builder.Build();

        // Status codes without a body (e.g. 400 for an unparseable request body) get a JSON problem document.
        app.UseStatusCodePages();

        // Token: protects /api/v1/* only; the static UI is public (slice plan, open question 6).
        if (!string.IsNullOrEmpty(token))
        {
            app.Use(async (ctx, next) =>
            {
                if (ctx.Request.Path.StartsWithSegments("/api/v1") && !Auth.TokenMatches(token, ctx.Request.Headers.Authorization.ToString()))
                {
                    ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    ctx.Response.Headers.WWWAuthenticate = "Bearer";
                    await ctx.Response.WriteAsJsonAsync(new ErrorBody("Unauthorized", 401), WebJsonContext.Default.ErrorBody);
                    return;
                }

                await next();
            });
        }

        var books = new Dictionary<string, Book>
        {
            ["B1"] = new Book(
                "B1",
                "Event Streams in Practice",
                ["Jördis Ålander"],
                [new FormatInfo("pdf", 1_048_576, 212), new FormatInfo("epub", 524_288, null)],
                ["tech/streams", "reference"]),
        };

        var api = app.MapGroup("/api/v1");

        api.MapGet("/books/{id}", Results<Ok<Book>, NotFound<ErrorBody>> (string id) =>
                books.TryGetValue(id, out var book) ? TypedResults.Ok(book) : TypedResults.NotFound(new ErrorBody($"Unknown book '{id}'", 404)))
            .WithName("GetBook")
            .Produces<Book>(StatusCodes.Status200OK)
            .Produces<ErrorBody>(StatusCodes.Status404NotFound);

        api.MapPut("/books/{id}/tags", Results<Ok<TagsResponse>, NotFound<ErrorBody>, BadRequest<ErrorBody>> (string id, TagsRequest body) =>
            {
                if (!books.TryGetValue(id, out var book))
                    return TypedResults.NotFound(new ErrorBody($"Unknown book '{id}'", 404));
                if (body.Tags is null || body.Tags.Any(string.IsNullOrWhiteSpace))
                    return TypedResults.BadRequest(new ErrorBody("tags must be a list of non-empty strings", 400));
                books[id] = book with { Tags = body.Tags };
                return TypedResults.Ok(new TagsResponse(id, body.Tags));
            })
            .WithName("PutTags")
            .Produces<TagsResponse>(StatusCodes.Status200OK)
            .Produces<ErrorBody>(StatusCodes.Status400BadRequest)
            .Produces<ErrorBody>(StatusCodes.Status404NotFound);

        api.MapGet("/files/{id}", (string id) =>
                file is not null && id == "F1"
                    ? Results.File(Path.GetFullPath(file), "application/octet-stream", enableRangeProcessing: true)
                    : Results.Json(new ErrorBody($"Unknown file '{id}'", 404), WebJsonContext.Default.ErrorBody, statusCode: 404))
            .WithName("GetFile")
            .Produces(StatusCodes.Status200OK, contentType: "application/octet-stream")
            .Produces(StatusCodes.Status206PartialContent, contentType: "application/octet-stream")
            .Produces<ErrorBody>(StatusCodes.Status404NotFound);

        // Unmatched /api/v1 paths are JSON 404s, never the SPA shell.
        api.MapFallback("/{**rest}", () => Results.Json(new ErrorBody("Not found", 404), WebJsonContext.Default.ErrorBody, statusCode: 404))
            .ExcludeFromDescription();

        app.MapOpenApi("/openapi/v1.json");

        // Embedded Svelte build (Ui/dist, logical folder "ui") served without any file on disk.
        var ui = new ManifestEmbeddedFileProvider(typeof(Program).Assembly, "ui");
        app.UseStaticFiles(new StaticFileOptions { FileProvider = ui });
        app.MapFallbackToFile("index.html", new StaticFileOptions { FileProvider = ui });

        app.Lifetime.ApplicationStarted.Register(() =>
        {
            var urls = app.Urls;
            Console.Error.WriteLine("listening on " + string.Join(",", urls));
        });

        await app.RunAsync();
        return 0;
    }
}
