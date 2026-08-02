using System.Collections.Concurrent;

namespace BunkerFlow.Integration.DeadLettering;

/// <summary>In-process dead-letter sink for tests and local runs.</summary>
public sealed class InMemoryDeadLetterSink : IDeadLetterSink
{
    private readonly ConcurrentQueue<DeadLetteredRecord> _deadLettered = new();

    public IReadOnlyCollection<DeadLetteredRecord> DeadLettered => _deadLettered;

    public Task SendAsync(DeadLetteredRecord deadLettered, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(deadLettered);
        cancellationToken.ThrowIfCancellationRequested();

        _deadLettered.Enqueue(deadLettered);
        return Task.CompletedTask;
    }
}
