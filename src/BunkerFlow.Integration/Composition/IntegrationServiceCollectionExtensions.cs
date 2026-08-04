using Azure.Messaging.ServiceBus;
using BunkerFlow.Integration.DeadLettering;
using BunkerFlow.Integration.Idempotency;
using BunkerFlow.Integration.Landing;
using BunkerFlow.Integration.Messaging;
using BunkerFlow.Integration.Normalization;
using BunkerFlow.Integration.Observability;
using BunkerFlow.Integration.Pipeline;
using BunkerFlow.Integration.Publishing;
using BunkerFlow.Integration.Resilience;
using BunkerFlow.Integration.Validation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace BunkerFlow.Integration.Composition;

/// <summary>
/// Wires the ingestion pipeline for both hosts. Everything is registered
/// against an interface, and the concrete implementation is chosen from
/// configuration, so the same code runs against real infrastructure in compose
/// and against in-memory fakes on a laptop with nothing installed.
/// </summary>
public static class IntegrationServiceCollectionExtensions
{
    public static IServiceCollection AddBunkerFlowIntegration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var serviceBusOptions = configuration.GetSection(ServiceBusOptions.SectionName)
            .Get<ServiceBusOptions>() ?? new ServiceBusOptions();
        var landingOptions = configuration.GetSection(LandingOptions.SectionName)
            .Get<LandingOptions>() ?? new LandingOptions();
        var postgresConnectionString = configuration.GetConnectionString("Postgres");

        services.AddSingleton(serviceBusOptions);
        services.AddSingleton(landingOptions);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<PipelineMetrics>();
        services.AddSingleton<IRecordNormalizer, BunkerTradeNormalizer>();
        services.AddSingleton<IEventValidator, BunkerTradeValidator>();
        services.AddSingleton(provider => RetryPolicy.Default(provider.GetRequiredService<TimeProvider>()));
        services.AddSingleton<IngestionPipeline>();

        AddMessaging(services, serviceBusOptions);
        AddPersistence(services, postgresConnectionString);
        AddLanding(services, landingOptions);

        return services;
    }

    private static void AddMessaging(IServiceCollection services, ServiceBusOptions options)
    {
        if (!options.IsConfigured)
        {
            // No broker configured: land in-process so the gateway is still
            // runnable and demonstrable, rather than crashing on startup.
            services.AddSingleton<IEventPublisher, LoopbackEventPublisher>();
            services.AddSingleton<InMemoryDeadLetterSink>();
            services.AddSingleton<IDeadLetterSink>(provider =>
                provider.GetRequiredService<InMemoryDeadLetterSink>());
            return;
        }

        // One client per credential, not one per process. Least-privilege rules
        // produce entity-scoped connection strings, so a client built for the
        // topic cannot address the dead-letter queue even though both only send.
        services.AddSingleton(_ => new ServiceBusClient(options.ConnectionString));
        services.AddSingleton<IEventPublisher>(provider => new ServiceBusEventPublisher(
            provider.GetRequiredService<ServiceBusClient>(),
            options));
        services.AddSingleton<IDeadLetterSink>(provider => new ServiceBusDeadLetterSink(
            new ServiceBusClient(options.EffectiveDeadLetterConnectionString),
            options,
            provider.GetRequiredService<ILogger<ServiceBusDeadLetterSink>>()));
    }

    private static void AddPersistence(IServiceCollection services, string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddSingleton<IDeduplicationStore, InMemoryDeduplicationStore>();
            services.AddSingleton<IEventRepository, InMemoryEventRepository>();
            return;
        }

        services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
        services.AddSingleton<IDeduplicationStore>(provider => new PostgresDeduplicationStore(
            provider.GetRequiredService<NpgsqlDataSource>()));
        services.AddSingleton<PostgresEventRepository>(provider => new PostgresEventRepository(
            provider.GetRequiredService<NpgsqlDataSource>()));
        services.AddSingleton<IEventRepository>(provider =>
            provider.GetRequiredService<PostgresEventRepository>());
    }

    private static void AddLanding(IServiceCollection services, LandingOptions options)
    {
        services.AddSingleton<ILandingWriter>(provider => new ParquetLandingWriter(
            options.ParquetRootPath,
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ILogger<ParquetLandingWriter>>()));
    }
}
