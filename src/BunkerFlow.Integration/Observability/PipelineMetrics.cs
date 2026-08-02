using System.Text;
using BunkerFlow.Contracts;
using BunkerFlow.Integration.Pipeline;

namespace BunkerFlow.Integration.Observability;

/// <summary>
/// Counters for what the pipeline actually did, rendered in Prometheus text
/// format at /metrics. Broken out by ingestion channel so a stalled batch
/// puller is visible even while the streaming path keeps working.
/// </summary>
public sealed class PipelineMetrics
{
    private readonly ConcurrentCounter _accepted = new();
    private readonly ConcurrentCounter _duplicate = new();
    private readonly ConcurrentCounter _rejected = new();
    private readonly ConcurrentCounter _failed = new();
    private readonly ConcurrentCounter _publishRetries = new();

    public void Record(IngestionOutcome outcome, IngestionChannel channel)
    {
        var counter = outcome switch
        {
            IngestionOutcome.Accepted => _accepted,
            IngestionOutcome.Duplicate => _duplicate,
            IngestionOutcome.Rejected => _rejected,
            IngestionOutcome.Failed => _failed,
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown outcome."),
        };

        counter.Increment(channel);
    }

    public void RecordPublishRetries(IngestionChannel channel, int retries)
    {
        if (retries > 0)
        {
            _publishRetries.Add(channel, retries);
        }
    }

    public long TotalFor(IngestionOutcome outcome) => outcome switch
    {
        IngestionOutcome.Accepted => _accepted.Total,
        IngestionOutcome.Duplicate => _duplicate.Total,
        IngestionOutcome.Rejected => _rejected.Total,
        IngestionOutcome.Failed => _failed.Total,
        _ => 0,
    };

    public string RenderPrometheus()
    {
        var builder = new StringBuilder();

        builder.AppendLine("# HELP bunkerflow_records_total Source records processed by outcome and channel.");
        builder.AppendLine("# TYPE bunkerflow_records_total counter");
        AppendOutcome(builder, "accepted", _accepted);
        AppendOutcome(builder, "duplicate", _duplicate);
        AppendOutcome(builder, "rejected", _rejected);
        AppendOutcome(builder, "failed", _failed);

        builder.AppendLine("# HELP bunkerflow_publish_retries_total Publish attempts that had to be retried.");
        builder.AppendLine("# TYPE bunkerflow_publish_retries_total counter");
        foreach (var channel in Enum.GetValues<IngestionChannel>())
        {
            builder.AppendLine(
                $"bunkerflow_publish_retries_total{{channel=\"{Label(channel)}\"}} {_publishRetries.For(channel)}");
        }

        return builder.ToString();
    }

    private static void AppendOutcome(StringBuilder builder, string outcome, ConcurrentCounter counter)
    {
        foreach (var channel in Enum.GetValues<IngestionChannel>())
        {
            builder.AppendLine(
                $"bunkerflow_records_total{{outcome=\"{outcome}\",channel=\"{Label(channel)}\"}} {counter.For(channel)}");
        }
    }

    private static string Label(IngestionChannel channel) => channel.ToString().ToLowerInvariant();

    /// <summary>One counter per channel, incremented without locking.</summary>
    private sealed class ConcurrentCounter
    {
        private readonly long[] _values = new long[Enum.GetValues<IngestionChannel>().Length];

        public long Total
        {
            get
            {
                long total = 0;
                for (var index = 0; index < _values.Length; index++)
                {
                    total += Interlocked.Read(ref _values[index]);
                }

                return total;
            }
        }

        public long For(IngestionChannel channel) => Interlocked.Read(ref _values[(int)channel]);

        public void Increment(IngestionChannel channel) => Add(channel, 1);

        public void Add(IngestionChannel channel, long amount) =>
            Interlocked.Add(ref _values[(int)channel], amount);
    }
}
