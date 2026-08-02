namespace BunkerFlow.Contracts;

/// <summary>
/// A raw record as it arrives from a source system, before normalization.
///
/// Fields stay as strings on purpose. Business systems hand over loosely typed
/// data (CSV exports, JSON APIs, stream payloads) and parsing is the
/// normalizer's job, not the transport's.
/// </summary>
public sealed record SourceRecord
{
    public required string SourceSystem { get; init; }

    /// <summary>The source system's own identifier, used as the dedupe key.</summary>
    public required string SourceRecordId { get; init; }

    /// <summary>How the record reached us. Useful for lineage and metrics.</summary>
    public required IngestionChannel Channel { get; init; }

    public required IReadOnlyDictionary<string, string?> Fields { get; init; }

    public string? Field(string name) =>
        Fields.TryGetValue(name, out var value) ? value : null;
}

public enum IngestionChannel
{
    /// <summary>Scheduled pull from a source system's REST endpoint.</summary>
    Batch,

    /// <summary>Real-time consumption from a Kafka topic.</summary>
    Stream,

    /// <summary>Pushed directly to the gateway's ingest API.</summary>
    Api,
}
