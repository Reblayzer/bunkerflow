using BunkerFlow.Contracts;
using BunkerFlow.Integration.Landing;
using Microsoft.Extensions.Logging;

namespace BunkerFlow.Integration.Publishing;

/// <summary>
/// Short-circuits the broker and lands events in-process.
///
/// This is the local development transport, used when no Service Bus
/// connection string is configured. It keeps a laptop run end to end
/// observable: ingest a record and it shows up on /events. It is deliberately
/// not the production path, because it gives up the durability, retry and
/// dead-lettering that Service Bus provides between the gateway and the
/// landing writer.
/// </summary>
public sealed class LoopbackEventPublisher : IEventPublisher
{
    private readonly IEventRepository _repository;
    private readonly ILogger<LoopbackEventPublisher> _logger;

    public LoopbackEventPublisher(
        IEventRepository repository,
        ILogger<LoopbackEventPublisher> logger)
    {
        _repository = repository;
        _logger = logger;

        _logger.LogWarning(
            "No Service Bus connection string configured. Events are landed in-process; " +
            "this mode is for local development only.");
    }

    public async Task PublishAsync(IntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        await _repository.AppendAsync(integrationEvent, cancellationToken).ConfigureAwait(false);
    }
}
