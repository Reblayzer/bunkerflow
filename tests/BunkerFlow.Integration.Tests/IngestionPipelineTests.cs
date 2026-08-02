using BunkerFlow.Contracts;
using BunkerFlow.Integration.DeadLettering;
using BunkerFlow.Integration.Errors;
using BunkerFlow.Integration.Idempotency;
using BunkerFlow.Integration.Normalization;
using BunkerFlow.Integration.Observability;
using BunkerFlow.Integration.Pipeline;
using BunkerFlow.Integration.Publishing;
using BunkerFlow.Integration.Resilience;
using BunkerFlow.Integration.Tests.TestSupport;
using BunkerFlow.Integration.Validation;
using Microsoft.Extensions.Logging.Abstractions;

namespace BunkerFlow.Integration.Tests;

public sealed class IngestionPipelineTests
{
    private readonly TestClock _clock = new();
    private readonly InMemoryDeduplicationStore _deduplicationStore = new();
    private readonly InMemoryDeadLetterSink _deadLetterSink = new();
    private readonly PipelineMetrics _metrics = new();

    [Fact]
    public async Task Should_publish_a_valid_record_and_report_it_accepted()
    {
        var publisher = ScriptedEventPublisher.AlwaysSucceeds();
        var pipeline = Build(publisher);

        var result = await pipeline.ProcessAsync(RecordBuilder.Valid().Build(), CancellationToken.None);

        Assert.Equal(IngestionOutcome.Accepted, result.Outcome);
        Assert.Single(publisher.Published);
        Assert.Empty(_deadLetterSink.DeadLettered);
        Assert.Equal(1, _metrics.TotalFor(IngestionOutcome.Accepted));
    }

    [Fact]
    public async Task Should_publish_a_replayed_record_only_once()
    {
        var publisher = ScriptedEventPublisher.AlwaysSucceeds();
        var pipeline = Build(publisher);
        var record = RecordBuilder.Valid().From("trading-desk", "TD-7").Build();

        var first = await pipeline.ProcessAsync(record, CancellationToken.None);
        var second = await pipeline.ProcessAsync(record, CancellationToken.None);

        Assert.Equal(IngestionOutcome.Accepted, first.Outcome);
        Assert.Equal(IngestionOutcome.Duplicate, second.Outcome);
        Assert.Single(publisher.Published);
        Assert.Empty(_deadLetterSink.DeadLettered);
    }

    [Fact]
    public async Task Should_dedupe_the_same_trade_arriving_on_two_different_channels()
    {
        var publisher = ScriptedEventPublisher.AlwaysSucceeds();
        var pipeline = Build(publisher);

        await pipeline.ProcessAsync(
            RecordBuilder.Valid().From("trading-desk", "TD-9").Via(IngestionChannel.Batch).Build(),
            CancellationToken.None);

        var viaStream = await pipeline.ProcessAsync(
            RecordBuilder.Valid().From("trading-desk", "TD-9").Via(IngestionChannel.Stream).Build(),
            CancellationToken.None);

        Assert.Equal(IngestionOutcome.Duplicate, viaStream.Outcome);
        Assert.Single(publisher.Published);
    }

    [Fact]
    public async Task Should_dead_letter_a_record_that_cannot_be_normalized()
    {
        var publisher = ScriptedEventPublisher.AlwaysSucceeds();
        var pipeline = Build(publisher);

        var result = await pipeline.ProcessAsync(
            RecordBuilder.Valid().Without("vesselImo").Build(),
            CancellationToken.None);

        Assert.Equal(IngestionOutcome.Rejected, result.Outcome);
        Assert.Equal("normalization_failed", result.Reason);
        Assert.Empty(publisher.Published);

        var deadLettered = Assert.Single(_deadLetterSink.DeadLettered);
        Assert.Equal("normalization_failed", deadLettered.Reason);
        Assert.Equal(TestClock.DefaultNow, deadLettered.DeadLetteredAtUtc);
    }

    [Fact]
    public async Task Should_dead_letter_a_record_that_fails_data_quality()
    {
        var publisher = ScriptedEventPublisher.AlwaysSucceeds();
        var pipeline = Build(publisher);

        var result = await pipeline.ProcessAsync(
            RecordBuilder.Valid().With("vesselImo", RecordBuilder.InvalidImo).Build(),
            CancellationToken.None);

        Assert.Equal(IngestionOutcome.Rejected, result.Outcome);
        Assert.Equal("validation_failed", result.Reason);
        Assert.Empty(publisher.Published);
        Assert.Single(_deadLetterSink.DeadLettered);
    }

    [Fact]
    public async Task Should_keep_a_rejected_business_key_free_for_a_corrected_resend()
    {
        var publisher = ScriptedEventPublisher.AlwaysSucceeds();
        var pipeline = Build(publisher);

        await pipeline.ProcessAsync(
            RecordBuilder.Valid().From("erp", "E-1").With("product", "JETA1").Build(),
            CancellationToken.None);

        var corrected = await pipeline.ProcessAsync(
            RecordBuilder.Valid().From("erp", "E-1").Build(),
            CancellationToken.None);

        Assert.Equal(IngestionOutcome.Accepted, corrected.Outcome);
        Assert.Single(publisher.Published);
    }

    [Fact]
    public async Task Should_retry_a_transient_publish_failure_and_still_accept_the_record()
    {
        var publisher = ScriptedEventPublisher.FailsTimes(
            2, new TransientPublishException("service bus timed out"));
        var pipeline = Build(publisher);

        var result = await pipeline.ProcessAsync(RecordBuilder.Valid().Build(), CancellationToken.None);

        Assert.Equal(IngestionOutcome.Accepted, result.Outcome);
        Assert.Equal(3, publisher.Attempts);
        Assert.Single(publisher.Published);
    }

    [Fact]
    public async Task Should_dead_letter_a_record_the_broker_keeps_refusing()
    {
        var publisher = ScriptedEventPublisher.AlwaysFailsWith(
            new TransientPublishException("service bus unreachable"));
        var pipeline = Build(publisher);

        var result = await pipeline.ProcessAsync(RecordBuilder.Valid().Build(), CancellationToken.None);

        Assert.Equal(IngestionOutcome.Failed, result.Outcome);
        Assert.Equal("publish_transient", result.Reason);
        Assert.Single(_deadLetterSink.DeadLettered);
    }

    [Fact]
    public async Task Should_release_the_business_key_when_publishing_never_succeeded()
    {
        var failing = ScriptedEventPublisher.AlwaysFailsWith(
            new PermanentPublishException("topic missing"));

        var firstAttempt = await Build(failing).ProcessAsync(
            RecordBuilder.Valid().From("trading-desk", "TD-13").Build(),
            CancellationToken.None);

        Assert.Equal(IngestionOutcome.Failed, firstAttempt.Outcome);

        // Same key, same store, now with a healthy broker: the trade must not be
        // written off as a duplicate just because the first publish failed.
        var healthy = ScriptedEventPublisher.AlwaysSucceeds();
        var replay = await Build(healthy).ProcessAsync(
            RecordBuilder.Valid().From("trading-desk", "TD-13").Build(),
            CancellationToken.None);

        Assert.Equal(IngestionOutcome.Accepted, replay.Outcome);
        Assert.Single(healthy.Published);
    }

    [Fact]
    public async Task Should_not_retry_a_publish_error_that_retrying_cannot_fix()
    {
        var publisher = ScriptedEventPublisher.AlwaysFailsWith(
            new PermanentPublishException("payload too large"));
        var pipeline = Build(publisher);

        var result = await pipeline.ProcessAsync(RecordBuilder.Valid().Build(), CancellationToken.None);

        Assert.Equal(IngestionOutcome.Failed, result.Outcome);
        Assert.Equal(1, publisher.Attempts);
    }

    [Fact]
    public async Task Should_count_outcomes_per_channel_for_the_metrics_endpoint()
    {
        var pipeline = Build(ScriptedEventPublisher.AlwaysSucceeds());

        await pipeline.ProcessAsync(
            RecordBuilder.Valid().From("erp", "E-1").Via(IngestionChannel.Batch).Build(),
            CancellationToken.None);
        await pipeline.ProcessAsync(
            RecordBuilder.Valid().From("kafka", "K-1").Via(IngestionChannel.Stream).Build(),
            CancellationToken.None);
        await pipeline.ProcessAsync(
            RecordBuilder.Valid().From("kafka", "K-2").Via(IngestionChannel.Stream)
                .With("port", "Fredericia").Build(),
            CancellationToken.None);

        var rendered = _metrics.RenderPrometheus();

        Assert.Contains("bunkerflow_records_total{outcome=\"accepted\",channel=\"batch\"} 1", rendered, StringComparison.Ordinal);
        Assert.Contains("bunkerflow_records_total{outcome=\"accepted\",channel=\"stream\"} 1", rendered, StringComparison.Ordinal);
        Assert.Contains("bunkerflow_records_total{outcome=\"rejected\",channel=\"stream\"} 1", rendered, StringComparison.Ordinal);
    }

    private IngestionPipeline Build(IEventPublisher publisher) => new(
        new BunkerTradeNormalizer(_clock),
        new BunkerTradeValidator(_clock),
        _deduplicationStore,
        publisher,
        _deadLetterSink,
        new RetryPolicy(
            maxAttempts: 3,
            baseDelay: TimeSpan.FromMilliseconds(1),
            maxDelay: TimeSpan.FromMilliseconds(5),
            TimeProvider.System,
            jitter: () => 0),
        _metrics,
        _clock,
        NullLogger<IngestionPipeline>.Instance);
}
