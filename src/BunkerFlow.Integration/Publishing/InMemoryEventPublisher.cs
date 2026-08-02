using System.Collections.Concurrent;
using BunkerFlow.Contracts;

namespace BunkerFlow.Integration.Publishing;

/// <summary>
/// Collects published events in memory. Backs the unit tests, and lets the API
/// run locally without a broker.
/// </summary>
public sealed class InMemoryEventPublisher : IEventPublisher
{
    private readonly ConcurrentQueue<IntegrationEvent> _published = new();

    public IReadOnlyCollection<IntegrationEvent> Published => _published;

    public Task PublishAsync(IntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        cancellationToken.ThrowIfCancellationRequested();

        _published.Enqueue(integrationEvent);
        return Task.CompletedTask;
    }
}
