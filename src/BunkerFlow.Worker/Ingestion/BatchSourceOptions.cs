namespace BunkerFlow.Worker.Ingestion;

/// <summary>One scheduled pull from one source system.</summary>
public sealed class BatchSourceOptions
{
    public const string SectionName = "BatchSources";

    public bool Enabled { get; set; } = true;

    /// <summary>Name recorded on every event this source produces.</summary>
    public string SourceSystem { get; set; } = string.Empty;

    /// <summary>REST endpoint returning a JSON array of flat records.</summary>
    public string Endpoint { get; set; } = string.Empty;

    public int IntervalSeconds { get; set; } = 60;

    /// <summary>Field in the payload holding the source system's own record id.</summary>
    public string RecordIdField { get; set; } = "sourceRecordId";

    /// <summary>How long a single pull may take before it is abandoned.</summary>
    public int TimeoutSeconds { get; set; } = 30;
}
