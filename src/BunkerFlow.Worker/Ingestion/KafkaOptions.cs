namespace BunkerFlow.Worker.Ingestion;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    public bool Enabled { get; set; }

    public string BootstrapServers { get; set; } = "localhost:9092";

    public string Topic { get; set; } = "bunker.trades.raw";

    public string ConsumerGroup { get; set; } = "bunkerflow-ingestion";

    /// <summary>Source system name stamped on events from this topic.</summary>
    public string SourceSystem { get; set; } = "port-telemetry";

    /// <summary>Field in the message payload holding the source record id.</summary>
    public string RecordIdField { get; set; } = "sourceRecordId";

    public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(BootstrapServers);
}
