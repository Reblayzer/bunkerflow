using System.Security.Cryptography;
using System.Text;

namespace BunkerFlow.Contracts;

/// <summary>
/// The common event contract every source is normalized into. Everything
/// downstream of the normalizer (Service Bus, the landing writer, the query
/// API) works with this shape and never with a source-specific one.
/// </summary>
public sealed record IntegrationEvent
{
    public const string BunkerTradeRecorded = "bunker.trade.recorded";

    /// <summary>
    /// Version of the payload shape. Producers stamp it, consumers refuse what
    /// they do not understand rather than guessing.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    public required string EventId { get; init; }

    public required string EventType { get; init; }

    public required int SchemaVersion { get; init; }

    public required string SourceSystem { get; init; }

    public required string SourceRecordId { get; init; }

    public required IngestionChannel Channel { get; init; }

    /// <summary>When the business event happened in the source system.</summary>
    public required DateTimeOffset OccurredAtUtc { get; init; }

    /// <summary>When the gateway accepted it. Set once, at normalization time.</summary>
    public required DateTimeOffset IngestedAtUtc { get; init; }

    public required BunkerTrade Payload { get; init; }

    /// <summary>
    /// The business key a consumer dedupes on. Source systems retry and replay,
    /// so the same trade can legitimately arrive more than once.
    /// </summary>
    public string DeduplicationKey => BuildDeduplicationKey(SourceSystem, SourceRecordId);

    public static string BuildDeduplicationKey(string sourceSystem, string sourceRecordId) =>
        $"{sourceSystem}:{sourceRecordId}";

    /// <summary>
    /// Derives a stable event id from the business key, so replaying the same
    /// source record produces the same id. That lets Service Bus duplicate
    /// detection catch repeats even before our own dedupe store sees them.
    /// </summary>
    public static string BuildEventId(string sourceSystem, string sourceRecordId)
    {
        var key = BuildDeduplicationKey(sourceSystem, sourceRecordId);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return new Guid(hash.AsSpan(0, 16)).ToString();
    }
}
