using BunkerFlow.Contracts;

namespace BunkerFlow.Integration.Pipeline;

public enum IngestionOutcome
{
    /// <summary>Normalized, validated, deduped and published.</summary>
    Accepted,

    /// <summary>Already seen. Not an error: sources replay.</summary>
    Duplicate,

    /// <summary>Bad data. Dead-lettered, will not be retried as-is.</summary>
    Rejected,

    /// <summary>Infrastructure problem. Dead-lettered and eligible for replay.</summary>
    Failed,
}

public sealed record IngestionResult
{
    public required IngestionOutcome Outcome { get; init; }

    public IntegrationEvent? Event { get; init; }

    /// <summary>Machine-readable reason, present for everything except Accepted.</summary>
    public string? Reason { get; init; }

    public string? Detail { get; init; }

    public static IngestionResult Accepted(IntegrationEvent integrationEvent) =>
        new() { Outcome = IngestionOutcome.Accepted, Event = integrationEvent };

    public static IngestionResult Duplicate(IntegrationEvent integrationEvent) =>
        new()
        {
            Outcome = IngestionOutcome.Duplicate,
            Event = integrationEvent,
            Reason = "duplicate",
            Detail = $"Business key '{integrationEvent.DeduplicationKey}' has already been ingested.",
        };

    public static IngestionResult Rejected(string reason, string detail, IntegrationEvent? integrationEvent = null) =>
        new()
        {
            Outcome = IngestionOutcome.Rejected,
            Event = integrationEvent,
            Reason = reason,
            Detail = detail,
        };

    public static IngestionResult Failed(string reason, string detail, IntegrationEvent? integrationEvent = null) =>
        new()
        {
            Outcome = IngestionOutcome.Failed,
            Event = integrationEvent,
            Reason = reason,
            Detail = detail,
        };
}
