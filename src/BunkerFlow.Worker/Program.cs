using BunkerFlow.Integration.Composition;
using BunkerFlow.Integration.Landing;
using BunkerFlow.Integration.Messaging;
using BunkerFlow.Worker.Ingestion;
using BunkerFlow.Worker.Landing;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddBunkerFlowIntegration(builder.Configuration);
builder.Services.AddHttpClient(nameof(BatchIngestionWorker));

AddBatchSources(builder);
AddKafkaIngestion(builder);
AddLanding(builder);

var host = builder.Build();

await InitializeLandingStoreAsync(host);
await host.RunAsync();

static void AddBatchSources(HostApplicationBuilder builder)
{
    var sources = builder.Configuration
        .GetSection(BatchSourceOptions.SectionName)
        .Get<List<BatchSourceOptions>>() ?? [];

    foreach (var source in sources.Where(candidate => candidate.Enabled))
    {
        // One hosted service per source, so a source that is down or slow only
        // delays its own schedule.
        builder.Services.AddSingleton<IHostedService>(provider => new BatchIngestionWorker(
            source,
            provider.GetRequiredService<IHttpClientFactory>(),
            provider.GetRequiredService<BunkerFlow.Integration.Pipeline.IngestionPipeline>(),
            provider.GetRequiredService<ILogger<BatchIngestionWorker>>()));
    }
}

static void AddKafkaIngestion(HostApplicationBuilder builder)
{
    var kafkaOptions = builder.Configuration
        .GetSection(KafkaOptions.SectionName)
        .Get<KafkaOptions>() ?? new KafkaOptions();

    builder.Services.AddSingleton(kafkaOptions);

    if (kafkaOptions.IsConfigured)
    {
        builder.Services.AddHostedService<KafkaIngestionWorker>();
    }
}

static void AddLanding(HostApplicationBuilder builder)
{
    var serviceBusOptions = builder.Configuration
        .GetSection(ServiceBusOptions.SectionName)
        .Get<ServiceBusOptions>() ?? new ServiceBusOptions();

    // Without a broker there is no subscription to read, so the worker runs
    // ingestion only and the API serves whatever the in-memory store holds.
    if (serviceBusOptions.IsConfigured)
    {
        builder.Services.AddHostedService<ServiceBusLandingWorker>();
    }
}

static async Task InitializeLandingStoreAsync(IHost host)
{
    var repository = host.Services.GetService<PostgresEventRepository>();
    if (repository is null)
    {
        return;
    }

    await repository.InitializeAsync(CancellationToken.None);
}
