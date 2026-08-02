using BunkerFlow.Contracts;

namespace BunkerFlow.Integration.Landing;

/// <summary>
/// The landing store, behind an interface so the API and the worker never
/// build queries themselves. Postgres backs it in the compose stack; the
/// in-memory implementation backs the tests and a no-database local run.
/// </summary>
public interface IEventRepository
{
    /// <summary>
    /// Appends an event. Implementations must be idempotent on event id, since
    /// Service Bus delivery is at-least-once.
    /// </summary>
    Task AppendAsync(IntegrationEvent integrationEvent, CancellationToken cancellationToken);

    Task<IReadOnlyList<IntegrationEvent>> QueryAsync(
        EventQuery query,
        CancellationToken cancellationToken);

    Task<long> CountAsync(CancellationToken cancellationToken);

    /// <summary>Backs the readiness probe.</summary>
    Task<bool> IsReachableAsync(CancellationToken cancellationToken);
}
