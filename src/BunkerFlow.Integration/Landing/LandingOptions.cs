namespace BunkerFlow.Integration.Landing;

public sealed class LandingOptions
{
    public const string SectionName = "Landing";

    /// <summary>
    /// Root of the Parquet store. A local path here; an abfss:// container
    /// under Databricks or Microsoft Fabric.
    /// </summary>
    public string ParquetRootPath { get; set; } = "data/landing";

    /// <summary>How many events to accumulate before writing a Parquet file.</summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>Longest a partial batch may wait before being flushed anyway.</summary>
    public int FlushIntervalSeconds { get; set; } = 15;
}
