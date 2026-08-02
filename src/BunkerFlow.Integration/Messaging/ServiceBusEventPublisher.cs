using Azure.Messaging.ServiceBus;
using BunkerFlow.Contracts;
using BunkerFlow.Integration.Errors;
using BunkerFlow.Integration.Publishing;

namespace BunkerFlow.Integration.Messaging;

/// <summary>
/// Publishes normalized events to a Service Bus topic.
///
/// Two things make this safe to retry. The message id is the deterministic
/// event id, so with duplicate detection enabled on the namespace a replay is
/// discarded by the broker itself. And Service Bus tells us whether a failure
/// is transient, which is mapped onto our own error types so the retry policy
/// does not waste attempts on something permanent.
/// </summary>
public sealed class ServiceBusEventPublisher : IEventPublisher, IAsyncDisposable
{
    private readonly ServiceBusSender _sender;

    public ServiceBusEventPublisher(ServiceBusClient client, ServiceBusOptions options)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);

        _sender = client.CreateSender(options.TopicName);
    }

    public async Task PublishAsync(IntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var message = new ServiceBusMessage(EventSerialization.Serialize(integrationEvent))
        {
            MessageId = integrationEvent.EventId,
            Subject = integrationEvent.EventType,
            ContentType = "application/json",
            CorrelationId = integrationEvent.DeduplicationKey,
        };

        // Promoted to application properties so subscriptions can filter on them
        // without deserializing the body.
        message.ApplicationProperties["sourceSystem"] = integrationEvent.SourceSystem;
        message.ApplicationProperties["channel"] = integrationEvent.Channel.ToString();
        message.ApplicationProperties["schemaVersion"] = integrationEvent.SchemaVersion;

        try
        {
            await _sender.SendMessageAsync(message, cancellationToken).ConfigureAwait(false);
        }
        catch (ServiceBusException exception) when (exception.IsTransient)
        {
            throw new TransientPublishException(
                $"Service Bus rejected event {integrationEvent.EventId} with a transient error " +
                $"({exception.Reason}).",
                exception);
        }
        catch (ServiceBusException exception)
        {
            throw new PermanentPublishException(
                $"Service Bus rejected event {integrationEvent.EventId} permanently " +
                $"({exception.Reason}).",
                exception);
        }
        catch (Exception exception) when (exception is TimeoutException or IOException)
        {
            throw new TransientPublishException(
                $"Could not reach Service Bus while publishing event {integrationEvent.EventId}.",
                exception);
        }
    }

    public async ValueTask DisposeAsync() => await _sender.DisposeAsync().ConfigureAwait(false);
}
