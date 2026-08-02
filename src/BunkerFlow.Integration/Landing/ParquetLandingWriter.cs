using BunkerFlow.Contracts;
using Microsoft.Extensions.Logging;
using Parquet.Serialization;

namespace BunkerFlow.Integration.Landing;

/// <summary>
/// Writes landed events as Parquet, partitioned by trade date, which is the
/// layout a lakehouse table expects.
///
/// In production this target is Databricks or Microsoft Fabric. Locally it is
/// the filesystem, so the same partitioning and file format can be exercised
/// end to end without a cloud subscription.
/// </summary>
public sealed class ParquetLandingWriter : ILandingWriter
{
    private readonly string _rootPath;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ParquetLandingWriter> _logger;

    public ParquetLandingWriter(
        string rootPath,
        TimeProvider timeProvider,
        ILogger<ParquetLandingWriter> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        _rootPath = rootPath;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task WriteAsync(
        IReadOnlyCollection<IntegrationEvent> events,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (events.Count == 0)
        {
            return;
        }

        // One file per partition per batch. Small files are a known lakehouse
        // problem; a real deployment compacts them on a schedule.
        foreach (var partition in events.GroupBy(candidate => candidate.OccurredAtUtc.UtcDateTime.Date))
        {
            var directory = Path.Combine(_rootPath, $"dt={partition.Key:yyyy-MM-dd}");
            Directory.CreateDirectory(directory);

            var fileName = $"events-{_timeProvider.GetUtcNow().UtcDateTime:yyyyMMddHHmmssfff}.parquet";
            var path = Path.Combine(directory, fileName);

            var rows = partition.Select(TradeRow.From).ToList();

            await using var stream = File.Create(path);
            await ParquetSerializer.SerializeAsync(rows, stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Landed {Count} event(s) to {Path}",
                rows.Count,
                path);
        }
    }

    /// <summary>
    /// Flattened projection of an integration event. Parquet columns are flat,
    /// and a flat row is also what a bronze-layer table looks like.
    /// </summary>
    internal sealed class TradeRow
    {
        public string EventId { get; set; } = string.Empty;

        public string EventType { get; set; } = string.Empty;

        public int SchemaVersion { get; set; }

        public string SourceSystem { get; set; } = string.Empty;

        public string SourceRecordId { get; set; } = string.Empty;

        public string Channel { get; set; } = string.Empty;

        public DateTime OccurredAtUtc { get; set; }

        public DateTime IngestedAtUtc { get; set; }

        public string TradeReference { get; set; } = string.Empty;

        public string VesselImo { get; set; } = string.Empty;

        public string Port { get; set; } = string.Empty;

        public string Product { get; set; } = string.Empty;

        public decimal QuantityMt { get; set; }

        public decimal PriceUsdPerMt { get; set; }

        public decimal TotalUsd { get; set; }

        public string Counterparty { get; set; } = string.Empty;

        public DateTime TradedAtUtc { get; set; }

        public static TradeRow From(IntegrationEvent integrationEvent) => new()
        {
            EventId = integrationEvent.EventId,
            EventType = integrationEvent.EventType,
            SchemaVersion = integrationEvent.SchemaVersion,
            SourceSystem = integrationEvent.SourceSystem,
            SourceRecordId = integrationEvent.SourceRecordId,
            Channel = integrationEvent.Channel.ToString(),
            OccurredAtUtc = integrationEvent.OccurredAtUtc.UtcDateTime,
            IngestedAtUtc = integrationEvent.IngestedAtUtc.UtcDateTime,
            TradeReference = integrationEvent.Payload.TradeReference,
            VesselImo = integrationEvent.Payload.VesselImo,
            Port = integrationEvent.Payload.Port,
            Product = integrationEvent.Payload.Product,
            QuantityMt = integrationEvent.Payload.QuantityMt,
            PriceUsdPerMt = integrationEvent.Payload.PriceUsdPerMt,
            TotalUsd = integrationEvent.Payload.TotalUsd,
            Counterparty = integrationEvent.Payload.Counterparty,
            TradedAtUtc = integrationEvent.Payload.TradedAtUtc.UtcDateTime,
        };
    }
}
