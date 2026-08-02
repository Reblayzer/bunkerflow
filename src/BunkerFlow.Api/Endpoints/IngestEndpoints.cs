using BunkerFlow.Contracts;
using BunkerFlow.Integration.Pipeline;

namespace BunkerFlow.Api.Endpoints;

/// <summary>
/// Push ingestion. The same pipeline the batch puller and the Kafka consumer
/// use, exposed over HTTP for source systems that would rather push.
/// </summary>
public static class IngestEndpoints
{
    /// <summary>Cap on a single batch, so an oversized payload is refused early.</summary>
    public const int MaxBatchSize = 500;

    public static void MapIngestEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/ingest").WithTags("Ingestion");

        group.MapPost("/", IngestOneAsync)
            .WithName("IngestRecord")
            .WithSummary("Ingest a single source record.");

        group.MapPost("/batch", IngestBatchAsync)
            .WithName("IngestBatch")
            .WithSummary("Ingest up to 500 source records in one call.");
    }

    private static async Task<IResult> IngestOneAsync(
        IngestRequest request,
        IngestionPipeline pipeline,
        CancellationToken cancellationToken)
    {
        if (!TryBuildRecord(request, out var record, out var error))
        {
            return Results.BadRequest(ApiResponse.Error("invalid_request", error));
        }

        var result = await pipeline.ProcessAsync(record, cancellationToken);
        return ToResult(result);
    }

    private static async Task<IResult> IngestBatchAsync(
        IngestRequest[] requests,
        IngestionPipeline pipeline,
        CancellationToken cancellationToken)
    {
        if (requests.Length == 0)
        {
            return Results.BadRequest(ApiResponse.Error("invalid_request", "The batch is empty."));
        }

        if (requests.Length > MaxBatchSize)
        {
            return Results.BadRequest(ApiResponse.Error(
                "batch_too_large",
                $"A batch may contain at most {MaxBatchSize} records, got {requests.Length}."));
        }

        var outcomes = new List<IngestionResult>(requests.Length);
        foreach (var request in requests)
        {
            if (!TryBuildRecord(request, out var record, out var error))
            {
                outcomes.Add(IngestionResult.Rejected("invalid_request", error));
                continue;
            }

            outcomes.Add(await pipeline.ProcessAsync(record, cancellationToken));
        }

        return Results.Ok(ApiResponse.Ok(new BatchIngestResponse(
            Accepted: outcomes.Count(outcome => outcome.Outcome == IngestionOutcome.Accepted),
            Duplicate: outcomes.Count(outcome => outcome.Outcome == IngestionOutcome.Duplicate),
            Rejected: outcomes.Count(outcome => outcome.Outcome == IngestionOutcome.Rejected),
            Failed: outcomes.Count(outcome => outcome.Outcome == IngestionOutcome.Failed))));
    }

    private static bool TryBuildRecord(
        IngestRequest request,
        out SourceRecord record,
        out string error)
    {
        record = null!;

        if (string.IsNullOrWhiteSpace(request.SourceSystem))
        {
            error = "sourceSystem is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.SourceRecordId))
        {
            error = "sourceRecordId is required.";
            return false;
        }

        if (request.Fields is null || request.Fields.Count == 0)
        {
            error = "fields is required and must not be empty.";
            return false;
        }

        record = new SourceRecord
        {
            SourceSystem = request.SourceSystem,
            SourceRecordId = request.SourceRecordId,
            Channel = IngestionChannel.Api,
            Fields = request.Fields,
        };

        error = string.Empty;
        return true;
    }

    private static IResult ToResult(IngestionResult result) => result.Outcome switch
    {
        IngestionOutcome.Accepted => Results.Accepted(
            $"/events?sourceSystem={result.Event!.SourceSystem}",
            ApiResponse.Ok(new IngestResponse(result.Outcome.ToString(), result.Event.EventId))),

        IngestionOutcome.Duplicate => Results.Ok(
            ApiResponse.Ok(new IngestResponse(result.Outcome.ToString(), result.Event!.EventId))),

        IngestionOutcome.Rejected => Results.UnprocessableEntity(
            ApiResponse.Error(result.Reason ?? "rejected", result.Detail ?? "The record was rejected.")),

        // The record is dead-lettered and replayable, so this is a 503, not a 500.
        _ => Results.Json(
            ApiResponse.Error(result.Reason ?? "failed", result.Detail ?? "The record could not be published."),
            statusCode: StatusCodes.Status503ServiceUnavailable),
    };
}

public sealed record IngestRequest(
    string SourceSystem,
    string SourceRecordId,
    Dictionary<string, string?> Fields);

public sealed record IngestResponse(string Outcome, string EventId);

public sealed record BatchIngestResponse(int Accepted, int Duplicate, int Rejected, int Failed);
