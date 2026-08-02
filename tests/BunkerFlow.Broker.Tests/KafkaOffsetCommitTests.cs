using BunkerFlow.Contracts;
using BunkerFlow.Integration.DeadLettering;
using BunkerFlow.Integration.Errors;
using BunkerFlow.Integration.Idempotency;
using BunkerFlow.Integration.Normalization;
using BunkerFlow.Integration.Observability;
using BunkerFlow.Integration.Pipeline;
using BunkerFlow.Integration.Publishing;
using BunkerFlow.Integration.Resilience;
using BunkerFlow.Integration.Validation;
using BunkerFlow.Worker.Ingestion;
using Microsoft.Extensions.Logging.Abstractions;

namespace BunkerFlow.Broker.Tests;

/// <summary>
/// The offset rule, proved against a real broker rather than argued about.
///
/// A record the gateway could not publish must not have its offset committed.
/// If it did, the trade would vanish during exactly the broker outage the
/// retry policy exists for, and no amount of unit testing the pipeline would
/// show it, because the bug lives in the consumer loop.
/// </summary>
[Collection(nameof(RedpandaCollection))]
public sealed class KafkaOffsetCommitTests
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(45);

    private const string ValidTrade = """
        {
          "sourceRecordId": "PT-1",
          "tradeReference": "TR-800001",
          "vesselImo": "9074729",
          "port": "NLRTM",
          "product": "VLSFO",
          "quantityMt": "1450.0",
          "priceUsdPerMt": "598.40",
          "counterparty": "Nordic Marine Fuels",
          "tradedAtUtc": "2026-08-01T11:15:00Z"
        }
        """;

    private readonly RedpandaFixture _broker;

    // Shared across both runs on purpose: a reservation left behind by the
    // failed attempt would make the retry look like a duplicate.
    private readonly InMemoryDeduplicationStore _deduplicationStore = new();
    private readonly InMemoryDeadLetterSink _deadLetterSink = new();

    public KafkaOffsetCommitTests(RedpandaFixture broker) => _broker = broker;

    [Fact]
    public async Task Should_not_commit_the_offset_when_the_record_could_not_be_published()
    {
        var topic = $"trades-fail-{Guid.NewGuid():N}";
        var group = $"group-{Guid.NewGuid():N}";

        await _broker.CreateTopicAsync(topic);
        _broker.Produce(topic, "PT-1", ValidTrade);

        var publisher = ScriptedPublisher.AlwaysFails();
        await RunWorkerUntilAsync(topic, group, publisher, () => publisher.Attempts >= 1);

        Assert.Empty(publisher.Published);
        Assert.Null(_broker.CommittedOffset(topic, group));
    }

    [Fact]
    public async Task Should_redeliver_and_land_the_record_once_the_broker_recovers()
    {
        var topic = $"trades-recover-{Guid.NewGuid():N}";
        var group = $"group-{Guid.NewGuid():N}";

        await _broker.CreateTopicAsync(topic);
        _broker.Produce(topic, "PT-1", ValidTrade);

        // First run: the bus is down, so nothing must be published or committed.
        var failing = ScriptedPublisher.AlwaysFails();
        await RunWorkerUntilAsync(topic, group, failing, () => failing.Attempts >= 1);

        Assert.Empty(failing.Published);
        Assert.Null(_broker.CommittedOffset(topic, group));

        // Second run: same consumer group, healthy bus. The record must come
        // back, be accepted, and only then have its offset committed.
        var healthy = ScriptedPublisher.AlwaysSucceeds();
        await RunWorkerUntilAsync(topic, group, healthy, () => healthy.Published.Count >= 1);

        var landed = Assert.Single(healthy.Published);
        Assert.Equal("port-telemetry", landed.SourceSystem);
        Assert.Equal("PT-1", landed.SourceRecordId);
        Assert.Equal(IngestionChannel.Stream, landed.Channel);
        Assert.Equal(1450.0m, landed.Payload.QuantityMt);

        await WaitUntilAsync(() => _broker.CommittedOffset(topic, group) is not null);
        Assert.Equal(1L, _broker.CommittedOffset(topic, group)!.Value);
    }

    [Fact]
    public async Task Should_commit_the_offset_for_a_record_it_rejected_on_data_quality()
    {
        var topic = $"trades-reject-{Guid.NewGuid():N}";
        var group = $"group-{Guid.NewGuid():N}";

        await _broker.CreateTopicAsync(topic);

        // Bad IMO check digit. Resending it would fail again, so unlike a
        // publish failure this one is committed and dead-lettered instead.
        _broker.Produce(topic, "PT-BAD", ValidTrade.Replace("9074729", "9074720", StringComparison.Ordinal));

        var publisher = ScriptedPublisher.AlwaysSucceeds();
        await RunWorkerUntilAsync(topic, group, publisher, () => _deadLetterSink.DeadLettered.Count >= 1);

        Assert.Empty(publisher.Published);
        Assert.Single(_deadLetterSink.DeadLettered);

        await WaitUntilAsync(() => _broker.CommittedOffset(topic, group) is not null);
        Assert.Equal(1L, _broker.CommittedOffset(topic, group)!.Value);
    }

    private async Task RunWorkerUntilAsync(
        string topic,
        string consumerGroup,
        ScriptedPublisher publisher,
        Func<bool> done)
    {
        var worker = new KafkaIngestionWorker(
            new KafkaOptions
            {
                Enabled = true,
                BootstrapServers = _broker.BootstrapServers,
                Topic = topic,
                ConsumerGroup = consumerGroup,
                SourceSystem = "port-telemetry",
                RecordIdField = "sourceRecordId",
            },
            BuildPipeline(publisher),
            NullLogger<KafkaIngestionWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);

        try
        {
            await WaitUntilAsync(done);
        }
        finally
        {
            // Close() inside the consumer loop's finally is what flushes any
            // commit, so the worker is always stopped before offsets are read.
            await worker.StopAsync(CancellationToken.None);
            worker.Dispose();
        }
    }

    private IngestionPipeline BuildPipeline(IEventPublisher publisher) => new(
        new BunkerTradeNormalizer(TimeProvider.System),
        new BunkerTradeValidator(TimeProvider.System),
        _deduplicationStore,
        publisher,
        _deadLetterSink,
        new RetryPolicy(
            maxAttempts: 2,
            baseDelay: TimeSpan.FromMilliseconds(10),
            maxDelay: TimeSpan.FromMilliseconds(50),
            TimeProvider.System,
            jitter: () => 0),
        new PipelineMetrics(),
        TimeProvider.System,
        NullLogger<IngestionPipeline>.Instance);

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(Patience);

        while (!condition())
        {
            if (timeout.IsCancellationRequested)
            {
                throw new TimeoutException($"Condition was not met within {Patience.TotalSeconds}s.");
            }

            await Task.Delay(200, CancellationToken.None);
        }
    }

    /// <summary>A publisher the test drives, standing in for Service Bus.</summary>
    private sealed class ScriptedPublisher : IEventPublisher
    {
        private readonly bool _succeeds;
        private readonly List<IntegrationEvent> _published = [];
        private int _attempts;

        private ScriptedPublisher(bool succeeds) => _succeeds = succeeds;

        public IReadOnlyList<IntegrationEvent> Published
        {
            get
            {
                lock (_published)
                {
                    return [.. _published];
                }
            }
        }

        public int Attempts => Volatile.Read(ref _attempts);

        public static ScriptedPublisher AlwaysSucceeds() => new(true);

        public static ScriptedPublisher AlwaysFails() => new(false);

        public Task PublishAsync(IntegrationEvent integrationEvent, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _attempts);

            if (!_succeeds)
            {
                return Task.FromException(new TransientPublishException("service bus unreachable"));
            }

            lock (_published)
            {
                _published.Add(integrationEvent);
            }

            return Task.CompletedTask;
        }
    }
}
