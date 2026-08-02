using System.Collections.Concurrent;
using BunkerFlow.Contracts;

namespace BunkerFlow.Integration.Landing;

/// <summary>
/// Landing store that keeps everything in the process. Lets the API run and be
/// demonstrated without Postgres, and backs the query tests.
/// </summary>
public sealed class InMemoryEventRepository : IEventRepository
{
    private readonly ConcurrentDictionary<string, IntegrationEvent> _events =
        new(StringComparer.Ordinal);

    public Task AppendAsync(IntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        cancellationToken.ThrowIfCancellationRequested();

        _events[integrationEvent.EventId] = integrationEvent;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<IntegrationEvent>> QueryAsync(
        EventQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        var normalized = query.Normalized();

        var matches = _events.Values
            .Where(candidate => Matches(candidate, normalized))
            .OrderByDescending(candidate => candidate.OccurredAtUtc)
            .ThenBy(candidate => candidate.EventId, StringComparer.Ordinal)
            .Skip(normalized.Offset)
            .Take(normalized.Limit)
            .ToList();

        return Task.FromResult<IReadOnlyList<IntegrationEvent>>(matches);
    }

    public Task<long> CountAsync(CancellationToken cancellationToken) =>
        Task.FromResult<long>(_events.Count);

    public Task<bool> IsReachableAsync(CancellationToken cancellationToken) => Task.FromResult(true);

    private static bool Matches(IntegrationEvent candidate, EventQuery query)
    {
        if (query.SourceSystem is not null
            && !string.Equals(candidate.SourceSystem, query.SourceSystem, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (query.Port is not null
            && !string.Equals(candidate.Payload.Port, query.Port, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (query.Product is not null
            && !string.Equals(candidate.Payload.Product, query.Product, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (query.FromUtc is not null && candidate.OccurredAtUtc < query.FromUtc)
        {
            return false;
        }

        return query.ToUtc is null || candidate.OccurredAtUtc <= query.ToUtc;
    }
}
