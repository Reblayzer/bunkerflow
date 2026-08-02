using BunkerFlow.Contracts;

namespace BunkerFlow.Integration.Landing;

/// <summary>
/// Writes a batch of landed events to the lakehouse-style store. Batching is
/// part of the contract: columnar formats are written per file, not per row.
/// </summary>
public interface ILandingWriter
{
    Task WriteAsync(IReadOnlyCollection<IntegrationEvent> events, CancellationToken cancellationToken);
}
