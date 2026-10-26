using System.Globalization;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Shelf.Index;
using Shelf.Library;

namespace Shelf.Web;

/// <summary>The endpoints of <c>/api/v1</c> that read and change the library (PRD 7.3). The rules are those of <see cref="LibraryService"/>.</summary>
internal static class Api
{
    private const int DefaultSearchLimit = 20;
    private const int DefaultSimilarLimit = 10;

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/works", (
                [FromServices] LibraryHandle library,
                string? q,
                string? sort,
                string? order,
                string? offset,
                string? limit) =>
            Run(WebJsonContext.Default.WorkList, () =>
            {
                var from = Whole(offset, "offset must not be negative.", 0);
                var count = Whole(limit, $"limit must be between 1 and {BrowseOptions.MaxLimit}.", BrowseOptions.DefaultLimit);
                var page = library.Service.Browse(q, sort ?? "title", order ?? "asc", from, count);
                return new WorkList(page.Total, from, count, [.. page.Items.Select(Item)], Facets(page.Facets));
            }))
            .WithName("ListWorks")
            .Produces<WorkList>(StatusCodes.Status200OK);

        api.MapGet("/works/{id}", ([FromServices] LibraryHandle library, string id) =>
                Run(WebJsonContext.Default.WorkDetail, () => Detail(library.Service, library.Service.GetBook(RequireId(id)))))
            .WithName("GetWork")
            .Produces<WorkDetail>(StatusCodes.Status200OK);

        api.MapGet("/works/{id}/similar", ([FromServices] LibraryHandle library, string id, string? limit) =>
                Run(WebJsonContext.Default.SimilarResponse, () => Similar(library.Service, RequireId(id), limit)))
            .WithName("SimilarWorks")
            .Produces<SimilarResponse>(StatusCodes.Status200OK);

        api.MapMethods("/works/{id}", ["PATCH"], ([FromServices] LibraryHandle library, string id, [FromBody] JsonObject patch) =>
                Run(WebJsonContext.Default.WorkDetail, () => Detail(library.Service, library.Service.EditMetadata(RequireId(id), patch))))
            .WithName("EditWork")
            .Accepts<JsonObject>("application/merge-patch+json", "application/json")
            .Produces<WorkDetail>(StatusCodes.Status200OK);

        api.MapPut("/works/{id}/tags", ([FromServices] LibraryHandle library, string id, [FromBody] TagsRequest body) =>
                Run(WebJsonContext.Default.TagSetResult, () => library.Service.SetTags(RequireId(id), TagList(body))))
            .WithName("SetWorkTags")
            .Produces<TagSetResult>(StatusCodes.Status200OK);

        api.MapMethods("/files/{id}", ["GET", "HEAD"], ([FromServices] LibraryHandle library, string id, bool? download) =>
                SendFile(library.Service, id, download ?? false))
            .WithName("GetFile")
            .Produces(StatusCodes.Status200OK, contentType: "application/octet-stream")
            .Produces(StatusCodes.Status206PartialContent, contentType: "application/octet-stream");

        api.MapPut("/files/{id}/progress", ([FromServices] LibraryHandle library, string id, [FromBody] ProgressRequest body) =>
                Run(WebJsonContext.Default.ReadingPosition, () => library.Service.SaveProgress(id, RequireLocator(body))))
            .WithName("SaveReadingProgress")
            .Produces<ReadingPosition>(StatusCodes.Status200OK);

        api.MapGet("/files/{id}/annotations", ([FromServices] LibraryHandle library, string id) =>
                Run(WebJsonContext.Default.AnnotationViewArray, () => library.Service.ListAnnotations(id)))
            .WithName("ListFileAnnotations")
            .Produces<AnnotationView[]>(StatusCodes.Status200OK);

        api.MapGet("/works/{id}/annotations", ([FromServices] LibraryHandle library, string id) =>
                Run(WebJsonContext.Default.AnnotationViewArray, () => library.Service.ListWorkAnnotations(RequireId(id))))
            .WithName("ListWorkAnnotations")
            .Produces<AnnotationView[]>(StatusCodes.Status200OK);

        api.MapPost("/files/{id}/annotations", ([FromServices] LibraryHandle library, string id, [FromBody] AnnotationRequest body) =>
                Run(WebJsonContext.Default.AnnotationView, () => library.Service.AddAnnotation(id, Draft(body)), StatusCodes.Status201Created))
            .WithName("AddAnnotation")
            .Produces<AnnotationView>(StatusCodes.Status201Created);

        api.MapMethods("/files/{id}/annotations/{annotation}", ["PATCH"], ([FromServices] LibraryHandle library, string id, string annotation, [FromBody] JsonObject patch) =>
                Run(WebJsonContext.Default.AnnotationView, () => library.Service.UpdateAnnotation(id, annotation, Changes(patch))))
            .WithName("EditAnnotation")
            .Accepts<JsonObject>("application/merge-patch+json", "application/json")
            .Produces<AnnotationView>(StatusCodes.Status200OK);

        api.MapDelete("/files/{id}/annotations/{annotation}", ([FromServices] LibraryHandle library, string id, string annotation) =>
                Run(WebJsonContext.Default.AnnotationView, () =>
                {
                    library.Service.DeleteAnnotation(id, annotation);
                    return null!;
                }, StatusCodes.Status204NoContent))
            .WithName("DeleteAnnotation")
            .Produces(StatusCodes.Status204NoContent);

        api.MapPost("/works/{id}/merge", ([FromServices] LibraryHandle library, string id, [FromBody] MergeRequest body) =>
                Run(WebJsonContext.Default.MergeOutcome, () => library.Service.MergeWorks(id, RequireWorkId(body.From, "from"))))
            .WithName("MergeWorks")
            .Produces<MergeOutcome>(StatusCodes.Status200OK);

        api.MapPost("/works/{id}/cite-key/suggest", ([FromServices] LibraryHandle library, string id) =>
                Run(WebJsonContext.Default.CiteKeySuggestion, () => library.Service.SuggestCiteKey(RequireId(id))))
            .WithName("SuggestCiteKey")
            .Produces<CiteKeySuggestion>(StatusCodes.Status200OK);

        api.MapPost("/works/{id}/enrich", ([FromServices] LibraryHandle library, string id, [FromBody] EnrichRequest? body) =>
                Run(WebJsonContext.Default.EnrichPreviewResult, () =>
                    library.Service.EnrichPreview(RequireId(id), OptionalText(body?.Source, "source"), OptionalFlag(body?.Overwrite, "overwrite") ?? false, OptionalWhole(body?.Pick, "pick"), CancellationToken.None)))
            .WithName("EnrichPreview")
            .Accepts<EnrichRequest>("application/json")
            .Produces<EnrichPreviewResult>(StatusCodes.Status200OK);

        api.MapPost("/works/{id}/enrich/apply", ([FromServices] LibraryHandle library, string id, [FromBody] EnrichRequest? body) =>
                Run(WebJsonContext.Default.EnrichApplyResult, () =>
                    library.Service.EnrichApply(RequireId(id), OptionalText(body?.Source, "source"), OptionalFlag(body?.Overwrite, "overwrite") ?? false, OptionalWhole(body?.Pick, "pick"), OptionalFlag(body?.Cover, "cover") ?? true, CancellationToken.None)))
            .WithName("EnrichApply")
            .Accepts<EnrichRequest>("application/json")
            .Produces<EnrichApplyResult>(StatusCodes.Status200OK);

        api.MapPost("/files/{id}/split", ([FromServices] LibraryHandle library, string id) =>
                Run(WebJsonContext.Default.SplitOutcome, () => library.Service.SplitFile(id)))
            .WithName("SplitFile")
            .Produces<SplitOutcome>(StatusCodes.Status200OK);

        api.MapMethods("/works/{id}/cover", ["GET", "HEAD"], ([FromServices] LibraryHandle library, string id) =>
                SendCover(library.Service, id))
            .WithName("GetCover")
            .Produces(StatusCodes.Status200OK, contentType: "image/jpeg")
            .Produces(StatusCodes.Status304NotModified);

        api.MapGet("/duplicates", ([FromServices] LibraryHandle library) =>
                Run(WebJsonContext.Default.DuplicateList, () => DuplicateGroups(library.Service)))
            .WithName("ListDuplicates")
            .Produces<DuplicateList>(StatusCodes.Status200OK);

        api.MapPost("/duplicates/keep", ([FromServices] LibraryHandle library, [FromBody] FileRequest body) =>
                Run(WebJsonContext.Default.DuplicateOutcome, () => library.Service.KeepDuplicate(RequireFileId(body))))
            .WithName("KeepDuplicate")
            .Produces<DuplicateOutcome>(StatusCodes.Status200OK);

        api.MapPost("/duplicates/trash", ([FromServices] LibraryHandle library, [FromBody] FileRequest body) =>
                Run(WebJsonContext.Default.DuplicateOutcome, () => library.Service.TrashDuplicate(RequireFileId(body))))
            .WithName("TrashDuplicate")
            .Produces<DuplicateOutcome>(StatusCodes.Status200OK);

        api.MapPost("/duplicates/ignore", ([FromServices] LibraryHandle library, [FromBody] FileRequest body) =>
                Run(WebJsonContext.Default.DuplicateOutcome, () => library.Service.IgnoreDuplicates(RequireFileId(body))))
            .WithName("IgnoreDuplicates")
            .Produces<DuplicateOutcome>(StatusCodes.Status200OK);

        api.MapGet("/duplicates/probable", ([FromServices] LibraryHandle library) =>
                Run(WebJsonContext.Default.ProbablePairsResult, () => library.Service.FindProbableDuplicates()))
            .WithName("ListProbableDuplicates")
            .Produces<ProbablePairsResult>(StatusCodes.Status200OK);

        api.MapPost("/duplicates/probable/ignore", ([FromServices] LibraryHandle library, [FromBody] PairRequest body) =>
                Run(WebJsonContext.Default.DuplicateOutcome, () => library.Service.IgnoreProbableDuplicates(RequireWorkId(body.A, "a"), RequireWorkId(body.B, "b"))))
            .WithName("IgnoreProbableDuplicates")
            .Produces<DuplicateOutcome>(StatusCodes.Status200OK);

        api.MapGet("/tags", ([FromServices] LibraryHandle library) =>
                Run(WebJsonContext.Default.TagTree, () => library.Service.ListTags()))
            .WithName("ListTags")
            .Produces<TagTree>(StatusCodes.Status200OK);

        api.MapPost("/tags/rename", ([FromServices] LibraryHandle library, [FromBody] TagMoveRequest body) =>
                Run(WebJsonContext.Default.TagMoveResult, () => library.Service.RenameTag(RequireText(body.From, "from"), RequireText(body.To, "to"))))
            .WithName("RenameTag")
            .Produces<TagMoveResult>(StatusCodes.Status200OK);

        api.MapPost("/tags/merge", ([FromServices] LibraryHandle library, [FromBody] TagMoveRequest body) =>
                Run(WebJsonContext.Default.TagMoveResult, () => library.Service.MergeTag(RequireText(body.From, "from"), RequireText(body.Into, "into"))))
            .WithName("MergeTag")
            .Produces<TagMoveResult>(StatusCodes.Status200OK);

        api.MapGet("/review", ([FromServices] LibraryHandle library) =>
                Run(WebJsonContext.Default.ReviewList, () => library.Service.ListReview()))
            .WithName("ListReview")
            .Produces<ReviewList>(StatusCodes.Status200OK);

        api.MapPost("/review/{id}/accept", ([FromServices] LibraryHandle library, string id, [FromBody] AcceptRequest? body) =>
                Run(WebJsonContext.Default.ReviewOutcome, () => library.Service.AcceptReview(id, OptionalWorkId(body?.Into))))
            .WithName("AcceptReview")
            .Produces<ReviewOutcome>(StatusCodes.Status200OK);

        api.MapPost("/review/{id}/dismiss", ([FromServices] LibraryHandle library, string id) =>
                Run(WebJsonContext.Default.ReviewOutcome, () => library.Service.DismissReview(id)))
            .WithName("DismissReview")
            .Produces<ReviewOutcome>(StatusCodes.Status200OK);

        api.MapGet("/organize", ([FromServices] LibraryHandle library, string? pattern) =>
                Run(WebJsonContext.Default.OrganizePreview, () => library.Service.PreviewOrganize(pattern)))
            .WithName("PreviewOrganize")
            .Produces<OrganizePreview>(StatusCodes.Status200OK);

        api.MapPost("/organize", ([FromServices] LibraryHandle library, [FromBody] OrganizeRequest? body) =>
                Run(WebJsonContext.Default.OrganizeOutcome, () => library.Service.ApplyOrganize(body?.Pattern is null ? null : RequireText(body.Pattern, "pattern"))))
            .WithName("ApplyOrganize")
            .Produces<OrganizeOutcome>(StatusCodes.Status200OK);

        api.MapPost("/undo", ([FromServices] LibraryHandle library) =>
                Run(WebJsonContext.Default.UndoOutcome, () => library.Service.UndoLast()))
            .WithName("Undo")
            .Produces<UndoOutcome>(StatusCodes.Status200OK);

        api.MapGet("/search", ([FromServices] LibraryHandle library, string? q, string? mode, string? level, string? limit) =>
                Run(WebJsonContext.Default.SearchResponse, () => Search(library.Service, q, mode ?? "fulltext", level ?? "work", limit)))
            .WithName("Search")
            .Produces<SearchResponse>(StatusCodes.Status200OK);
    }

    /// <summary>
    /// The file with range processing (a part of it for a <c>Range</c> header, <c>416</c> beyond the end), <c>ETag</c> and
    /// <c>Last-Modified</c>. Inline for the reader; a download names the file.
    /// </summary>
    private static IResult SendFile(LibraryService service, string id, bool download)
    {
        try
        {
            var file = service.ResolveFile(id);
            var type = file.Format switch
            {
                "epub" => "application/epub+zip",
                "pdf" => "application/pdf",
                _ => "application/octet-stream",
            };
            return Serve(file, type, download);
        }
        catch (LibraryException e)
        {
            return Problem(e);
        }
    }

    /// <summary>The cover of a work as the image it is (<c>image/jpeg</c>, <c>image/png</c>, ...), with <c>ETag</c> and <c>Last-Modified</c>.</summary>
    private static IResult SendCover(LibraryService service, string id)
    {
        try
        {
            var cover = service.ResolveCover(RequireId(id));
            var type = cover.Format switch
            {
                "jpg" => "image/jpeg",
                "png" => "image/png",
                "gif" => "image/gif",
                "webp" => "image/webp",
                _ => "application/octet-stream",
            };
            return Serve(cover, type, download: false);
        }
        catch (LibraryException e)
        {
            return Problem(e);
        }
    }

    private static IResult Serve(FileLocation file, string type, bool download)
    {
        var modified = new DateTimeOffset(file.LastWriteUtc, TimeSpan.Zero);

        // Size and modification time: a file that was replaced has another ETag even if the clock did not move far.
        var tag = new EntityTagHeaderValue($"\"{file.Size.ToString("x", CultureInfo.InvariantCulture)}-{modified.UtcTicks.ToString("x", CultureInfo.InvariantCulture)}\"");
        return new RangeProblemResult(Results.File(file.Path, type, download ? file.Name : null, modified, tag, enableRangeProcessing: true));
    }

    private static DuplicateList DuplicateGroups(LibraryService service)
    {
        var groups = service.Duplicates();
        return new DuplicateList(
            groups.Count,
            [.. groups.Select(g => new DuplicateGroupItem(
                g.Kind == DuplicateKind.Identical ? "identical" : "same-format",
                g.Format,
                g.Keep,
                [.. g.Files.Select(f => new DuplicateFileItem(
                    FileIds.Make(f.Work.ToString(), f.Name),
                    f.Work.ToString(),
                    f.Title,
                    f.Name,
                    f.Path,
                    f.Size,
                    f.Pages,
                    f.Completeness))]))]);
    }

    private static string RequireFileId(FileRequest body) =>
        body.FileId is { } node && node.GetValueKind() == System.Text.Json.JsonValueKind.String && node.GetValue<string>() is { Length: > 0 } id
            ? id
            : throw new LibraryException("fileId is required: the ID of a file, as in the files of a work");

    private static string? OptionalText(JsonNode? node, string name) =>
        node is null ? null
        : node.GetValueKind() == System.Text.Json.JsonValueKind.String ? node.GetValue<string>()
        : throw new LibraryException($"{name} must be text");

    private static bool? OptionalFlag(JsonNode? node, string name) =>
        node is null ? null
        : node.GetValueKind() is System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False ? node.GetValue<bool>()
        : throw new LibraryException($"{name} must be true or false");

    private static int? OptionalWhole(JsonNode? node, string name) =>
        node is null ? null
        : node.GetValueKind() == System.Text.Json.JsonValueKind.Number && node.AsValue().TryGetValue<int>(out var number) ? number
        : throw new LibraryException($"{name} must be a whole number");

    private static string RequireWorkId(System.Text.Json.Nodes.JsonNode? node, string name) =>
        node is not null && node.GetValueKind() == System.Text.Json.JsonValueKind.String && node.GetValue<string>() is { Length: > 0 } id
            ? id
            : throw new LibraryException($"{name} is required: the ID of a work");

    private static string? OptionalWorkId(JsonNode? node) =>
        node is null ? null : RequireWorkId(node, "into");

    private static string RequireText(JsonNode? node, string name) =>
        node is not null && node.GetValueKind() == System.Text.Json.JsonValueKind.String && node.GetValue<string>() is { Length: > 0 } text
            ? text
            : throw new LibraryException($"{name} is required: text");

    private static string RequireLocator(ProgressRequest body) =>
        body.Locator is not null && body.Locator.GetValueKind() == System.Text.Json.JsonValueKind.String && body.Locator.GetValue<string>() is { Length: > 0 } locator
            ? locator
            : throw new LibraryException("locator is required: the place in the file, as text");

    private static WorkDetail Detail(LibraryService service, BookDetails b) =>
        new(
            b.Id,
            b.Title,
            b.Subtitle,
            b.Authors,
            b.Language,
            b.Year,
            b.Isbn13,
            b.Publisher,
            b.Series,
            b.SeriesIndex,
            b.Edition,
            b.CiteKey,
            b.Description,
            b.Rating,
            b.Tags,
            b.ReadStatus,
            service.HasCover(b.Id),
            [.. b.Files.Select(f => new WorkFile(FileIds.Make(b.Id, f.Name), f.Name, f.Format, f.SizeBytes, f.Pages, f.Progress))],
            service.PreferredFile(b.Files) is { } preferred ? FileIds.Make(b.Id, preferred) : null,
            b.Toc);

    private static SearchResponse Search(LibraryService service, string? q, string mode, string level, string? limit)
    {
        if (string.IsNullOrEmpty(q))
        {
            throw new LibraryException("q is required");
        }

        if (mode is not ("fulltext" or "semantic" or "hybrid"))
        {
            throw new LibraryException("mode must be fulltext, semantic or hybrid.");
        }

        var count = Whole(limit, $"limit must be between 1 and {LibraryService.MaxLimit}.", DefaultSearchLimit);
        if (mode == "fulltext")
        {
            var found = service.SearchBooks(q, count);
            return new SearchResponse(
                q,
                mode,
                "work",
                found.Total,
                0,
                0,
                null,
                [.. found.Results.Select(r => new SearchItem(r.Id, r.Title, r.Authors, r.Year, r.Language, r.Formats, r.Score, [.. r.Passages.Select(p => Passage(r.Id, p))]))]);
        }

        var semantic = service.SemanticSearch(q, level, hybrid: mode == "hybrid", count);

        // At the level of passages every hit is a passage, and one book can have several: the answer has one result per book,
        // in the order of its best passage, with all its passages.
        var books = new List<SearchItem>();
        foreach (var hit in semantic.Results)
        {
            var passages = hit.Passage is { } p ? new[] { Passage(hit.Id, p) } : [];
            var at = books.FindIndex(b => b.Id == hit.Id);
            if (at < 0)
            {
                books.Add(new SearchItem(hit.Id, hit.Title, hit.Authors, hit.Year, hit.Language, hit.Formats, hit.Score, passages));
            }
            else
            {
                books[at] = books[at] with { Passages = [.. books[at].Passages, .. passages] };
            }
        }

        return new SearchResponse(
            semantic.Query,
            mode,
            semantic.Level,
            books.Count,
            semantic.PendingFiles,
            semantic.WorksWithoutEmbedding,
            semantic.Note,
            [.. books]);
    }

    private static SimilarResponse Similar(LibraryService service, string id, string? limit)
    {
        var similar = service.SimilarBooks(id, Whole(limit, $"limit must be between 1 and {LibraryService.MaxLimit}.", DefaultSimilarLimit));
        return new SimilarResponse(
            similar.Id,
            similar.Title,
            similar.Total,
            similar.Note,
            [.. similar.Results.Select(r => new SearchItem(r.Id, r.Title, r.Authors, r.Year, r.Language, r.Formats, r.Score, []))]);
    }

    private static Passage Passage(string work, PassageRef p) =>
        new(FileIds.Make(work, Path.GetFileName(p.File)), p.File, p.Format, p.Location, p.Where, p.Snippet);

    /// <summary>The ID of a work, or a refusal. The service also reads folder and file paths; a web client must not probe the disk with them.</summary>
    private static string RequireId(string id) =>
        WorkLookup.LooksLikeId(id) ? id : throw new LibraryException($"no work matches '{id}'", LibraryErrorKind.NotFound);

    private static string[] TagList(TagsRequest body)
    {
        if (body.Tags is not JsonArray array || array.Any(t => t is null || t.GetValueKind() != System.Text.Json.JsonValueKind.String))
        {
            throw new LibraryException("tags must be a list of text");
        }

        return [.. array.Select(t => t!.GetValue<string>())];
    }

    /// <summary>A whole number from the query string, or <paramref name="fallback"/> when it is missing; <paramref name="message"/> when it is not a number.</summary>
    private static int Whole(string? text, string message, int fallback)
    {
        if (text is null)
        {
            return fallback;
        }

        return int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number) ? number : throw new LibraryException(message);
    }

    private static AnnotationDraft Draft(AnnotationRequest body)
    {
        string? exact = null, prefix = null, suffix = null;
        if (body.Quote is not null)
        {
            if (body.Quote is not JsonObject quote)
            {
                throw new LibraryException("quote must be an object with the text in exact");
            }

            exact = OptionalText(quote["exact"], "quote.exact");
            prefix = OptionalText(quote["prefix"], "quote.prefix");
            suffix = OptionalText(quote["suffix"], "quote.suffix");
        }

        return new AnnotationDraft(
            OptionalText(body.Kind, "kind"),
            OptionalText(body.Locator, "locator"),
            exact,
            prefix,
            suffix,
            OptionalText(body.Color, "color"),
            OptionalText(body.Note, "note"),
            OptionalRects(body.Rects));
    }

    private static double[][]? OptionalRects(JsonNode? node)
    {
        if (node is null)
        {
            return null;
        }

        const string Message = "rects must be a list of areas, each four numbers: x, y, width, height (fractions of the page)";
        if (node is not JsonArray array)
        {
            throw new LibraryException(Message);
        }

        return [.. array.Select(area =>
        {
            if (area is not JsonArray numbers || numbers.Count != 4 || numbers.Any(n => n is null || n.GetValueKind() != System.Text.Json.JsonValueKind.Number))
            {
                throw new LibraryException(Message);
            }

            return numbers.Select(n => n!.GetValue<double>()).ToArray();
        })];
    }

    /// <summary>Only the note and the color can change; what a highlight marks and where it is stays.</summary>
    private static AnnotationChanges Changes(JsonObject patch)
    {
        foreach (var (name, _) in patch)
        {
            if (name is not ("note" or "color"))
            {
                throw new LibraryException($"{name} cannot be changed; only note and color can");
            }
        }

        var hasNote = patch.ContainsKey("note");
        var hasColor = patch.ContainsKey("color");
        return new AnnotationChanges(hasNote, OptionalText(patch["note"], "note"), hasColor, OptionalText(patch["color"], "color"));
    }

    private static IResult Run<T>(JsonTypeInfo<T> info, Func<T> call, int status = StatusCodes.Status200OK)
    {
        try
        {
            var result = call();
            return status == StatusCodes.Status204NoContent
                ? Results.NoContent()
                : Results.Json(result, info, statusCode: status);
        }
        catch (LibraryException e)
        {
            return Problem(e);
        }
        catch (IndexException e)
        {
            return new ProblemResult(StatusCodes.Status500InternalServerError, e.Message);
        }
    }

    private static ProblemResult Problem(LibraryException e) =>
        new(
            e.Kind switch
            {
                LibraryErrorKind.NotFound => StatusCodes.Status404NotFound,
                LibraryErrorKind.Conflict => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status400BadRequest,
            },
            e.Message);

    private static WorkItem Item(WorkListEntry w) =>
        new(w.Id.ToString(), w.Title, w.Subtitle, [.. w.Authors], w.Year, w.Language, [.. w.Formats], [.. w.Tags], w.ReadStatus, w.HasCover);

    private static Facets Facets(BrowseFacets f) =>
        new(Values(f.Tags), Values(f.Authors), Values(f.Languages), Values(f.Years), Values(f.Formats), Values(f.Statuses));

    private static FacetValue[] Values(IReadOnlyList<FacetCount> counts) => [.. counts.Select(c => new FacetValue(c.Value, c.Count))];
}
