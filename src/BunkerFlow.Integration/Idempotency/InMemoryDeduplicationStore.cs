using System.Collections.Concurrent;

namespace BunkerFlow.Integration.Idempotency;

/// <summary>
/// Process-local dedupe store. Used by the tests and by the local
/// docker-compose run when Postgres is not wired up; the Postgres
/// implementation is the one that survives a restart.
/// </summary>
public sealed class InMemoryDeduplicationStore : IDeduplicationStore
{
    private readonly ConcurrentDictionary<string, byte> _reserved = new(StringComparer.Ordinal);

    public int Count => _reserved.Count;

    public Task<bool> TryReserveAsync(string deduplicationKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deduplicationKey);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(_reserved.TryAdd(deduplicationKey, 0));
    }

    public Task ReleaseAsync(string deduplicationKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deduplicationKey);
        cancellationToken.ThrowIfCancellationRequested();

        _reserved.TryRemove(deduplicationKey, out _);
        return Task.CompletedTask;
    }
}
