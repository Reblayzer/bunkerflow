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
                var message = consumer.Consume(stoppingToken);
                if (message?.Message is null)
                {
                    continue;
                }

                await HandleAsync(message.Message.Value, stoppingToken);

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

    private async Task HandleAsync(string payload, CancellationToken stoppingToken)
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
            return;
        }

        if (fields is null
            || !fields.TryGetValue(_options.RecordIdField, out var recordId)
            || string.IsNullOrWhiteSpace(recordId))
        {
            _logger.LogWarning(
                "Discarding a Kafka message without '{Field}'", _options.RecordIdField);
            return;
        }

        var record = new SourceRecord
        {
            SourceSystem = _options.SourceSystem,
            SourceRecordId = recordId,
            Channel = IngestionChannel.Stream,
            Fields = fields,
        };

        await _pipeline.ProcessAsync(record, stoppingToken);
    }
}
