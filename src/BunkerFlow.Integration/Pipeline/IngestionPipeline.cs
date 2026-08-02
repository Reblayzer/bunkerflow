using BunkerFlow.Contracts;
using BunkerFlow.Integration.DeadLettering;
using BunkerFlow.Integration.Errors;
using BunkerFlow.Integration.Idempotency;
using BunkerFlow.Integration.Normalization;
using BunkerFlow.Integration.Observability;
using BunkerFlow.Integration.Publishing;
using BunkerFlow.Integration.Resilience;
using BunkerFlow.Integration.Validation;
using Microsoft.Extensions.Logging;

namespace BunkerFlow.Integration.Pipeline;

/// <summary>
/// The single path every record takes, whatever channel it arrived on:
/// normalize, validate, claim the business key, publish, keep the claim.
///
/// Batch, streaming and API ingestion all call this. That is the point of the
/// design: one place where the integration rules live, three thin adapters in
/// front of it.
/// </summary>
public sealed class IngestionPipeline
{
    private readonly IRecordNormalizer _normalizer;
    private readonly IEventValidator _validator;
    private readonly IDeduplicationStore _deduplicationStore;
    private readonly IEventPublisher _publisher;
    private readonly IDeadLetterSink _deadLetterSink;
    private readonly RetryPolicy _retryPolicy;
    private readonly PipelineMetrics _metrics;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<IngestionPipeline> _logger;

    public IngestionPipeline(
        IRecordNormalizer normalizer,
        IEventValidator validator,
        IDeduplicationStore deduplicationStore,
        IEventPublisher publisher,
        IDeadLetterSink deadLetterSink,
        RetryPolicy retryPolicy,
        PipelineMetrics metrics,
        TimeProvider timeProvider,
        ILogger<IngestionPipeline> logger)
    {
        _normalizer = normalizer;
        _validator = validator;
        _deduplicationStore = deduplicationStore;
        _publisher = publisher;
        _deadLetterSink = deadLetterSink;
        _retryPolicy = retryPolicy;
        _metrics = metrics;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<IngestionResult> ProcessAsync(
        SourceRecord record,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);

        var result = await RunAsync(record, cancellationToken).ConfigureAwait(false);

        if (result.Outcome is IngestionOutcome.Rejected or IngestionOutcome.Failed)
        {
            await DeadLetterAsync(record, result, cancellationToken).ConfigureAwait(false);
        }

        _metrics.Record(result.Outcome, record.Channel);
        return result;
    }

    private async Task<IngestionResult> RunAsync(
        SourceRecord record,
        CancellationToken cancellationToken)
    {
        IntegrationEvent integrationEvent;
        try
        {
            integrationEvent = _normalizer.Normalize(record);
        }
        catch (NormalizationException exception)
        {
            _logger.LogWarning(
                exception,
                "Normalization rejected {SourceSystem}/{SourceRecordId}",
                record.SourceSystem,
                record.SourceRecordId);

            return IngestionResult.Rejected(exception.Code, exception.Message);
        }

        var validation = _validator.Validate(integrationEvent);
        if (!validation.IsValid)
        {
            _logger.LogWarning(
                "Validation rejected {SourceSystem}/{SourceRecordId}: {Failures}",
                record.SourceSystem,
                record.SourceRecordId,
                validation.Summary());

            return IngestionResult.Rejected("validation_failed", validation.Summary(), integrationEvent);
        }

        var key = integrationEvent.DeduplicationKey;
        var reserved = await _deduplicationStore
            .TryReserveAsync(key, cancellationToken)
            .ConfigureAwait(false);

        if (!reserved)
        {
            _logger.LogDebug("Skipping duplicate {DeduplicationKey}", key);
            return IngestionResult.Duplicate(integrationEvent);
        }

        try
        {
            var attempts = await _retryPolicy
                .ExecuteAsync(token => _publisher.PublishAsync(integrationEvent, token), cancellationToken)
                .ConfigureAwait(false);

            _metrics.RecordPublishRetries(record.Channel, attempts - 1);

            _logger.LogInformation(
                "Ingested {EventId} ({SourceSystem}/{SourceRecordId}) from {Channel} in {Attempts} attempt(s)",
                integrationEvent.EventId,
                record.SourceSystem,
                record.SourceRecordId,
                record.Channel,
                attempts);

            return IngestionResult.Accepted(integrationEvent);
        }
        catch (IntegrationException exception)
        {
            // The record never reached the bus, so give the key back. Leaving it
            // claimed would make a retry look like a duplicate and lose the trade.
            await ReleaseQuietlyAsync(key, cancellationToken).ConfigureAwait(false);

            _logger.LogError(
                exception,
                "Publish failed for {SourceSystem}/{SourceRecordId}",
                record.SourceSystem,
                record.SourceRecordId);

            return IngestionResult.Failed(exception.Code, exception.Message, integrationEvent);
        }
    }

    private async Task ReleaseQuietlyAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            await _deduplicationStore.ReleaseAsync(key, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A stuck reservation is bad but not worth masking the publish error.
            _logger.LogError(exception, "Could not release dedupe reservation {DeduplicationKey}", key);
        }
    }

    private async Task DeadLetterAsync(
        SourceRecord record,
        IngestionResult result,
        CancellationToken cancellationToken)
    {
        var deadLettered = new DeadLetteredRecord
        {
            Record = record,
            Reason = result.Reason ?? "unknown",
            Detail = result.Detail ?? string.Empty,
            DeadLetteredAtUtc = _timeProvider.GetUtcNow(),
        };

        try
        {
            await _deadLetterSink.SendAsync(deadLettered, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(
                exception,
                "Could not dead-letter {SourceSystem}/{SourceRecordId}",
                record.SourceSystem,
                record.SourceRecordId);
        }
    }
}
