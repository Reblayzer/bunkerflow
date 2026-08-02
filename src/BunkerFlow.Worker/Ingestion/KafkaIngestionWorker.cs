using System.Text.Json;
using BunkerFlow.Contracts;
using BunkerFlow.Integration.Pipeline;
using Confluent.Kafka;

namespace BunkerFlow.Worker.Ingestion;

/// <summary>
/// The streaming half: consume raw trade messages from Kafka and feed them to
/// the same pipeline the batch puller uses.
///
/// Offsets are committed only after the pipeline has taken responsibility for a
/// message. Combined with the dedupe store that gives at-least-once delivery
/// with exactly-once landing, which is the practical target.
/// </summary>
public sealed class KafkaIngestionWorker : BackgroundService
{
    /// <summary>How long to wait before retrying after a recoverable broker error.</summary>
    private static readonly TimeSpan RecoverableErrorDelay = TimeSpan.FromSeconds(5);

    private readonly KafkaOptions _options;
    private readonly IngestionPipeline _pipeline;
    private readonly ILogger<KafkaIngestionWorker> _logger;

    public KafkaIngestionWorker(
        KafkaOptions options,
        IngestionPipeline pipeline,
        ILogger<KafkaIngestionWorker> logger)
    {
        _options = options;
        _pipeline = pipeline;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.IsConfigured)
        {
            _logger.LogInformation("Kafka ingestion is disabled");
            return Task.CompletedTask;
        }

        // Consume() blocks, so the loop gets its own thread instead of tying up
        // a thread-pool thread for the lifetime of the process.
        return Task.Factory.StartNew(
            () => ConsumeLoopAsync(stoppingToken),
            stoppingToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default).Unwrap();
    }

    private async Task ConsumeLoopAsync(CancellationToken stoppingToken)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = _options.ConsumerGroup,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            EnablePartitionEof = false,
            AllowAutoCreateTopics = true,
        };

        using var consumer = new ConsumerBuilder<string, string>(config)
            .SetErrorHandler((_, error) =>
                _logger.LogError("Kafka error {Code}: {Reason}", error.Code, error.Reason))
            .Build();

        consumer.Subscribe(_options.Topic);
        _logger.LogInformation(
            "Consuming {Topic} from {BootstrapServers} as {Group}",
            _options.Topic,
            _options.BootstrapServers,
            _options.ConsumerGroup);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, string>? message;
                try
                {
                    message = consumer.Consume(stoppingToken);
                }
                catch (ConsumeException exception) when (!exception.Error.IsFatal)
                {
                    // The usual case is the topic not existing yet because the
                    // producer has not started. That is a normal startup race,
                    // not a reason to take the worker process down.
                    _logger.LogWarning(
                        "Kafka consume failed ({Reason}); retrying in {Delay}s",
                        exception.Error.Reason,
                        RecoverableErrorDelay.TotalSeconds);

                    await Task.Delay(RecoverableErrorDelay, stoppingToken);
                    continue;
                }

                if (message?.Message is null)
                {
                    continue;
                }

                var outcome = await HandleAsync(message.Message.Value, stoppingToken);

                if (outcome == IngestionOutcome.Failed)
                {
                    // The record never reached the bus. Leaving the offset
                    // uncommitted and seeking back means the broker hands it to
                    // us again once the infrastructure recovers, instead of the
                    // trade disappearing with the outage.
                    _logger.LogWarning(
                        "Not committing offset {Offset}: the record could not be published",
                        message.TopicPartitionOffset);

                    consumer.Seek(message.TopicPartitionOffset);
                    await Task.Delay(RecoverableErrorDelay, stoppingToken);
                    continue;
                }

                // Committing after handling means a crash replays the message
                // rather than losing it. The dedupe store absorbs the replay.
                consumer.Commit(message);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Kafka consumer stopping");
        }
        finally
        {
            consumer.Close();
        }
    }

    /// <returns>
    /// The pipeline's outcome, or null when the message was not a record at all
    /// and there is nothing to retry.
    /// </returns>
    private async Task<IngestionOutcome?> HandleAsync(string payload, CancellationToken stoppingToken)
    {
        Dictionary<string, string?>? fields;
        try
        {
            fields = JsonSerializer.Deserialize<Dictionary<string, string?>>(
                payload, EventSerialization.Options);
        }
        catch (JsonException exception)
        {
            // Unparseable bytes cannot be dead-lettered as a SourceRecord, since
            // there is no record to speak of. Log and move on.
            _logger.LogError(exception, "Discarding a Kafka message that is not JSON");
            return null;
        }

        if (fields is null
            || !fields.TryGetValue(_options.RecordIdField, out var recordId)
            || string.IsNullOrWhiteSpace(recordId))
        {
            _logger.LogWarning(
                "Discarding a Kafka message without '{Field}'", _options.RecordIdField);
            return null;
        }

        var record = new SourceRecord
        {
            SourceSystem = _options.SourceSystem,
            SourceRecordId = recordId,
            Channel = IngestionChannel.Stream,
            Fields = fields,
        };

        var result = await _pipeline.ProcessAsync(record, stoppingToken);
        return result.Outcome;
    }
}
