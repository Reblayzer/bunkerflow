using BunkerFlow.Api;
using BunkerFlow.Api.Endpoints;
using BunkerFlow.Api.Security;
using BunkerFlow.Contracts;
using BunkerFlow.Integration.Composition;
using BunkerFlow.Integration.Landing;

var builder = WebApplication.CreateBuilder(args);

var apiKeyOptions = builder.Configuration.GetSection(ApiKeyOptions.SectionName)
    .Get<ApiKeyOptions>() ?? new ApiKeyOptions();
builder.Services.AddSingleton(apiKeyOptions);
builder.Services.AddSingleton<ApiKeyEndpointFilter>();

// One JSON configuration everywhere, so what the API returns matches what goes
// on the wire between the workers.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = EventSerialization.Options.PropertyNamingPolicy;
    options.SerializerOptions.DefaultIgnoreCondition = EventSerialization.Options.DefaultIgnoreCondition;
    foreach (var converter in EventSerialization.Options.Converters)
    {
        options.SerializerOptions.Converters.Add(converter);
    }
});

builder.Services.AddBunkerFlowIntegration(builder.Configuration);
builder.Services.AddOpenApi();

var app = builder.Build();

if (!apiKeyOptions.IsEnabled)
{
    app.Logger.LogWarning(
        "No API keys configured. /ingest and /events are unauthenticated; " +
        "set Api__Keys__0 before exposing this anywhere.");
}

await InitializeLandingStoreAsync(app);

app.MapOpenApi();
app.MapIngestEndpoints();
app.MapEventEndpoints();
app.MapOperationalEndpoints();
app.MapMockSourceEndpoints();

app.MapGet("/", () => Results.Ok(ApiResponse.Ok(new
{
    service = "BunkerFlow integration gateway",
    endpoints = new[]
    {
        "/ingest", "/ingest/batch", "/events", "/health/live", "/health/ready",
        "/metrics", "/openapi/v1.json",
    },
})));

await app.RunAsync();

static async Task InitializeLandingStoreAsync(WebApplication app)
{
    var repository = app.Services.GetService<PostgresEventRepository>();
    if (repository is null)
    {
        return;
    }

    await repository.InitializeAsync(CancellationToken.None);
}

/// <summary>Exposed so the endpoint tests can start the API in-process.</summary>
public partial class Program;
