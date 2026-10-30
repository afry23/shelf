using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Shelf.Library;

namespace Shelf.Web;

/// <summary>An answer that is not a success: RFC 9457 fields, nothing else (no <c>type</c> URL, no trace ID).</summary>
public sealed record ApiProblem(int Status, string Title, string? Detail);

/// <summary>What <c>GET /api/v1/status</c> answers.</summary>
public sealed record StatusResponse(string Version, int Works, int Files);

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ApiProblem))]
[JsonSerializable(typeof(StatusResponse))]
[JsonSerializable(typeof(WorkList))]
[JsonSerializable(typeof(SearchResponse))]
[JsonSerializable(typeof(SimilarResponse))]
[JsonSerializable(typeof(TagSuggestionsResponse))]
[JsonSerializable(typeof(QuoteRequest))]
[JsonSerializable(typeof(PassageCitation))]
[JsonSerializable(typeof(WorkDetail))]
[JsonSerializable(typeof(DuplicateList))]
[JsonSerializable(typeof(DuplicateOutcome))]
[JsonSerializable(typeof(SplitOutcome))]
[JsonSerializable(typeof(ProbablePairsResult))]
[JsonSerializable(typeof(PairRequest))]
[JsonSerializable(typeof(MergeRequest))]
[JsonSerializable(typeof(MergeOutcome))]
[JsonSerializable(typeof(DeleteOutcome))]
[JsonSerializable(typeof(MoveRequest))]
[JsonSerializable(typeof(MoveFileOutcome))]
[JsonSerializable(typeof(AddFileOutcome))]
[JsonSerializable(typeof(CreateWorkOutcome))]
[JsonSerializable(typeof(FetchRequest))]
[JsonSerializable(typeof(ClipRequest))]
[JsonSerializable(typeof(ClipArticleOutcome))]
[JsonSerializable(typeof(FetchPaperOutcome))]
[JsonSerializable(typeof(CiteKeySuggestion))]
[JsonSerializable(typeof(EnrichRequest))]
[JsonSerializable(typeof(ReferencesFetchRequest))]
[JsonSerializable(typeof(ReferencesResult))]
[JsonSerializable(typeof(StatsResult))]
[JsonSerializable(typeof(ReferencesFetchResult))]
[JsonSerializable(typeof(EnrichPreviewResult))]
[JsonSerializable(typeof(EnrichApplyResult))]
[JsonSerializable(typeof(OrganizeRequest))]
[JsonSerializable(typeof(OrganizePreview))]
[JsonSerializable(typeof(OrganizeOutcome))]
[JsonSerializable(typeof(UndoOutcome))]
[JsonSerializable(typeof(AcceptRequest))]
[JsonSerializable(typeof(ReviewList))]
[JsonSerializable(typeof(ReviewOutcome))]
[JsonSerializable(typeof(TagMoveRequest))]
[JsonSerializable(typeof(TagMoveResult))]
[JsonSerializable(typeof(ProgressRequest))]
[JsonSerializable(typeof(ReadingPosition))]
[JsonSerializable(typeof(AnnotationRequest))]
[JsonSerializable(typeof(AnnotationView))]
[JsonSerializable(typeof(AnnotationView[]))]
[JsonSerializable(typeof(FileRequest))]
[JsonSerializable(typeof(TagTree))]
[JsonSerializable(typeof(TagSetResult))]
[JsonSerializable(typeof(TagsRequest))]
[JsonSerializable(typeof(JsonObject))]
internal partial class WebJsonContext : JsonSerializerContext;
