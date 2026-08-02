using Azure.Messaging.ServiceBus;
using BunkerFlow.Contracts;
using BunkerFlow.Integration.Landing;
using BunkerFlow.Integration.Messaging;

namespace BunkerFlow.Worker.Landing;

/// <summary>
/// Reads the Service Bus subscription and lands what it finds: a row in
/// Postgres for the query API, and a Parquet file for the lakehouse-style
/// store.
///
/// A message that cannot be deserialized or written is dead-lettered on the
/// subscription with a reason, which is Service Bus doing the work rather than
/// us reinventing it.
/// </summary>
public sealed class ServiceBusLandingWorker : BackgroundService
{
    private readonly ServiceBusClient _client;
    private readonly ServiceBusOptions _serviceBusOptions;
    private readonly LandingOptions _landingOptions;
    private readonly IEventRepository _repository;
    private readonly ILandingWriter _landingWriter;
    private readonly ILogger<ServiceBusLandingWorker> _logger;

    private readonly List<IntegrationEvent> _pending = [];
    private readonly SemaphoreSlim _pendingLock = new(1, 1);

    private ServiceBusProcessor? _processor;

    public ServiceBusLandingWorker(
        ServiceBusClient client,
        ServiceBusOptions serviceBusOptions,
        LandingOptions landingOptions,
        IEventRepository repository,
        ILandingWriter landingWriter,
        ILogger<ServiceBusLandingWorker> logger)
    {
        _client = client;
        _serviceBusOptions = serviceBusOptions;
        _landingOptions = landingOptions;
        _repository = repository;
        _landingWriter = landingWriter;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _processor = _client.CreateProcessor(
            _serviceBusOptions.TopicName,
            _serviceBusOptions.SubscriptionName,
            new ServiceBusProcessorOptions
            {
                AutoCompleteMessages = false,
                MaxConcurrentCalls = 4,
            });

        _processor.ProcessMessageAsync += OnMessageAsync;
        _processor.ProcessErrorAsync += OnErrorAsync;

        await _processor.StartProcessingAsync(stoppingToken);
        _logger.LogInformation(
            "Landing worker reading {Topic}/{Subscription}",
            _serviceBusOptions.TopicName,
            _serviceBusOptions.SubscriptionName);

        using var flushTimer = new PeriodicTimer(
            TimeSpan.FromSeconds(_landingOptions.FlushIntervalSeconds));

        try
        {
            while (await flushTimer.WaitForNextTickAsync(stoppingToken))
            {
                await FlushAsync(force: true, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down: write out whatever is buffered.
            await FlushAsync(force: true, CancellationToken.None);
        }
    }

    private async Task OnMessageAsync(ProcessMessageEventArgs args)
    {
        IntegrationEvent? integrationEvent;
        try
        {
            integrationEvent = EventSerialization.Deserialize(args.Message.Body.ToString());
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await args.DeadLetterMessageAsync(
                args.Message,
                "deserialization_failed",
                exception.Message,
                args.CancellationToken);
            return;
        }

        if (integrationEvent is null)
        {
            await args.DeadLetterMessageAsync(
                args.Message,
                "empty_payload",
                "The message body did not contain an integration event.",
                args.CancellationToken);
            return;
        }

        try
        {
            await _repository.AppendAsync(integrationEvent, args.CancellationToken);
            await BufferAsync(integrationEvent, args.CancellationToken);
            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Could not land event {EventId}", integrationEvent.EventId);

            // Abandon rather than dead-letter: this looks like an infrastructure
            // problem, and Service Bus will redeliver until MaxDeliveryCount
            // sends it to the dead-letter queue on its own.
            await args.AbandonMessageAsync(args.Message, cancellationToken: args.CancellationToken);
        }
    }

    private Task OnErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(
            args.Exception,
            "Service Bus processor error in {Operation} on {Entity}",
            args.ErrorSource,
            args.EntityPath);

        return Task.CompletedTask;
    }

    private async Task BufferAsync(IntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        await _pendingLock.WaitAsync(cancellationToken);
        try
        {
            _pending.Add(integrationEvent);
        }
        finally
        {
            _pendingLock.Release();
        }

        await FlushAsync(force: false, cancellationToken);
    }

    private async Task FlushAsync(bool force, CancellationToken cancellationToken)
    {
        List<IntegrationEvent> batch;

        await _pendingLock.WaitAsync(cancellationToken);
        try
        {
            if (_pending.Count == 0 || (!force && _pending.Count < _landingOptions.BatchSize))
            {
                return;
            }

            batch = [.. _pending];
            _pending.Clear();
        }
        finally
        {
            _pendingLock.Release();
        }

        await _landingWriter.WriteAsync(batch, cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_processor is not null)
        {
            await _processor.StopProcessingAsync(cancellationToken);
            await _processor.DisposeAsync();
        }

        await base.StopAsync(cancellationToken);
    }
}
