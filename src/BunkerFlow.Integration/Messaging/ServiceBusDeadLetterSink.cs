using System.Text.Json;
using Azure.Messaging.ServiceBus;
using BunkerFlow.Contracts;
using BunkerFlow.Integration.DeadLettering;
using Microsoft.Extensions.Logging;

namespace BunkerFlow.Integration.Messaging;

/// <summary>
/// Sends rejected records to a dedicated dead-letter queue, keeping the
/// original payload and the reason so they can be inspected and replayed
/// after the source data is corrected.
/// </summary>
public sealed class ServiceBusDeadLetterSink : IDeadLetterSink, IAsyncDisposable
{
    private readonly ServiceBusSender _sender;
    private readonly ILogger<ServiceBusDeadLetterSink> _logger;

    public ServiceBusDeadLetterSink(
        ServiceBusClient client,
        ServiceBusOptions options,
        ILogger<ServiceBusDeadLetterSink> logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);

        _sender = client.CreateSender(options.DeadLetterQueueName);
        _logger = logger;
    }

    public async Task SendAsync(DeadLetteredRecord deadLettered, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(deadLettered);

        var body = JsonSerializer.Serialize(deadLettered, EventSerialization.Options);

        var message = new ServiceBusMessage(body)
        {
            ContentType = "application/json",
            Subject = deadLettered.Reason,
            CorrelationId = IntegrationEvent.BuildDeduplicationKey(
                deadLettered.Record.SourceSystem,
                deadLettered.Record.SourceRecordId),
        };

        message.ApplicationProperties["reason"] = deadLettered.Reason;
        message.ApplicationProperties["sourceSystem"] = deadLettered.Record.SourceSystem;
        message.ApplicationProperties["channel"] = deadLettered.Record.Channel.ToString();

        try
        {
            await _sender.SendMessageAsync(message, cancellationToken).ConfigureAwait(false);
        }
        catch (ServiceBusException exception)
        {
            // Losing the dead letter is bad, but throwing here would mask the
            // original rejection. Log loudly and let the caller carry on.
            _logger.LogError(
                exception,
                "Could not write {SourceSystem}/{SourceRecordId} to the dead-letter queue",
                deadLettered.Record.SourceSystem,
                deadLettered.Record.SourceRecordId);
        }
    }

    public async ValueTask DisposeAsync() => await _sender.DisposeAsync().ConfigureAwait(false);
}
