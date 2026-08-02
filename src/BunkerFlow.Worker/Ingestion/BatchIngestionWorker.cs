using System.Net.Http.Json;
using BunkerFlow.Contracts;
using BunkerFlow.Integration.Pipeline;

namespace BunkerFlow.Worker.Ingestion;

/// <summary>
/// The batch half of the ingestion story: pull a source system's REST endpoint
/// on a schedule and push every record through the same pipeline the streaming
/// path uses.
///
/// A failed pull is logged and retried on the next tick rather than killing the
/// worker. One slow source system must not stop the others.
/// </summary>
public sealed class BatchIngestionWorker : BackgroundService
{
    private readonly BatchSourceOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IngestionPipeline _pipeline;
    private readonly ILogger<BatchIngestionWorker> _logger;

    public BatchIngestionWorker(
        BatchSourceOptions options,
        IHttpClientFactory httpClientFactory,
        IngestionPipeline pipeline,
        ILogger<BatchIngestionWorker> logger)
    {
        _options = options;
        _httpClientFactory = httpClientFactory;
        _pipeline = pipeline;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Batch source {SourceSystem} is disabled", _options.SourceSystem);
            return;
        }

        _logger.LogInformation(
            "Polling {SourceSystem} at {Endpoint} every {Interval}s",
            _options.SourceSystem,
            _options.Endpoint,
            _options.IntervalSeconds);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.IntervalSeconds));

        do
        {
            try
            {
                await PullOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Pull from {SourceSystem} failed, retrying on the next tick",
                    _options.SourceSystem);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task PullOnceAsync(CancellationToken stoppingToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

        var client = _httpClientFactory.CreateClient(nameof(BatchIngestionWorker));

        var records = await client.GetFromJsonAsync<List<Dictionary<string, string?>>>(
            _options.Endpoint,
            EventSerialization.Options,
            timeout.Token);

        if (records is null || records.Count == 0)
        {
            _logger.LogDebug("{SourceSystem} returned no records", _options.SourceSystem);
            return;
        }

        var outcomes = new Dictionary<IngestionOutcome, int>();

        foreach (var fields in records)
        {
            stoppingToken.ThrowIfCancellationRequested();

            if (!fields.TryGetValue(_options.RecordIdField, out var recordId)
                || string.IsNullOrWhiteSpace(recordId))
            {
                _logger.LogWarning(
                    "Skipping a {SourceSystem} record without '{Field}'",
                    _options.SourceSystem,
                    _options.RecordIdField);
                continue;
            }

            var record = new SourceRecord
            {
                SourceSystem = _options.SourceSystem,
                SourceRecordId = recordId,
                Channel = IngestionChannel.Batch,
                Fields = fields,
            };

            var result = await _pipeline.ProcessAsync(record, stoppingToken);
            outcomes[result.Outcome] = outcomes.GetValueOrDefault(result.Outcome) + 1;
        }

        _logger.LogInformation(
            "Pulled {Count} record(s) from {SourceSystem}: {Outcomes}",
            records.Count,
            _options.SourceSystem,
            string.Join(", ", outcomes.Select(entry => $"{entry.Key}={entry.Value}")));
    }
}
