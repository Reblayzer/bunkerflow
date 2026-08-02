using BunkerFlow.Contracts;

namespace BunkerFlow.Integration.DeadLettering;

/// <summary>
/// A record that could not be processed, kept with enough context to work out
/// why without going back to the source system.
/// </summary>
public sealed record DeadLetteredRecord
{
    public required SourceRecord Record { get; init; }

    /// <summary>Machine-readable reason, for example normalization_failed.</summary>
    public required string Reason { get; init; }

    public required string Detail { get; init; }

    public required DateTimeOffset DeadLetteredAtUtc { get; init; }
}

/// <summary>
/// Where records go when the pipeline gives up on them. Nothing is dropped
/// silently: a rejected record is inspectable and replayable.
/// </summary>
public interface IDeadLetterSink
{
    Task SendAsync(DeadLetteredRecord deadLettered, CancellationToken cancellationToken);
}
