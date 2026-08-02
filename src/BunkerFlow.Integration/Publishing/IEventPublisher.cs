using BunkerFlow.Contracts;

namespace BunkerFlow.Integration.Publishing;

/// <summary>
/// Publishes a normalized event onto the integration bus. The pipeline only
/// knows this interface, so the Service Bus client, an in-memory fake and any
/// future broker are interchangeable.
/// </summary>
public interface IEventPublisher
{
    /// <exception cref="Errors.TransientPublishException">Worth retrying.</exception>
    /// <exception cref="Errors.PermanentPublishException">Not worth retrying.</exception>
    Task PublishAsync(IntegrationEvent integrationEvent, CancellationToken cancellationToken);
}
