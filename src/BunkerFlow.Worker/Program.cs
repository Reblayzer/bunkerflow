using BunkerFlow.Integration.Composition;
using BunkerFlow.Integration.Landing;
using BunkerFlow.Integration.Messaging;
using BunkerFlow.Integration.Observability;
using BunkerFlow.Integration.Pipeline;
using BunkerFlow.Worker.Ingestion;
using BunkerFlow.Worker.Landing;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBunkerFlowIntegration(builder.Configuration);
builder.Services.AddHttpClient(nameof(BatchIngestionWorker));

AddBatchSources(builder);
AddKafkaIngestion(builder);
AddLanding(builder);

var app = builder.Build();

await InitializeLandingStoreAsync(app);

// The worker ingests most of the volume, so its counters are the ones worth
// scraping. Each process exposes its own; a Prometheus job targets both.
app.MapGet("/health/live", () => Results.Ok(new { ok = true, data = new { status = "live" } }));

app.MapGet("/metrics", (PipelineMetrics metrics) =>
    Results.Text(metrics.RenderPrometheus(), "text/plain; version=0.0.4"));

await app.RunAsync();

static void AddBatchSources(WebApplicationBuilder builder)
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
            provider.GetRequiredService<IngestionPipeline>(),
            provider.GetRequiredService<ILogger<BatchIngestionWorker>>()));
    }
}

static void AddKafkaIngestion(WebApplicationBuilder builder)
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

static void AddLanding(WebApplicationBuilder builder)
{
    var serviceBusOptions = builder.Configuration
        .GetSection(ServiceBusOptions.SectionName)
        .Get<ServiceBusOptions>() ?? new ServiceBusOptions();

    // Without a broker there is no subscription to read, so the worker runs
    // ingestion only and the API serves whatever the in-process store holds.
    if (serviceBusOptions.IsConfigured)
    {
        builder.Services.AddHostedService<ServiceBusLandingWorker>();
    }
}

static async Task InitializeLandingStoreAsync(WebApplication app)
{
    var repository = app.Services.GetService<PostgresEventRepository>();
    if (repository is null)
    {
        return;
    }

    await repository.InitializeAsync(CancellationToken.None);
}
