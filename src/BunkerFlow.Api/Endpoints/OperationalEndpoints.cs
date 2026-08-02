using BunkerFlow.Integration.Landing;
using BunkerFlow.Integration.Observability;

namespace BunkerFlow.Api.Endpoints;

/// <summary>
/// The endpoints an operator and a scheduler care about: is the process up, is
/// it able to serve, and what has it been doing.
/// </summary>
public static class OperationalEndpoints
{
    public static void MapOperationalEndpoints(this IEndpointRouteBuilder routes)
    {
        // Liveness answers "is the process running", nothing more. Tying it to a
        // dependency would make Kubernetes restart a healthy pod when the
        // database blips.
        routes.MapGet("/health/live", () => Results.Ok(ApiResponse.Ok(new { status = "live" })))
            .WithTags("Operations")
            .WithName("HealthLive");

        routes.MapGet("/health/ready", ReadyAsync)
            .WithTags("Operations")
            .WithName("HealthReady");

        routes.MapGet("/metrics", (PipelineMetrics metrics) =>
                Results.Text(metrics.RenderPrometheus(), "text/plain; version=0.0.4"))
            .WithTags("Operations")
            .WithName("Metrics")
            .WithSummary("Pipeline counters in Prometheus exposition format.");
    }

    private static async Task<IResult> ReadyAsync(
        IEventRepository repository,
        CancellationToken cancellationToken)
    {
        var landingReachable = await repository.IsReachableAsync(cancellationToken);

        if (!landingReachable)
        {
            return Results.Json(
                ApiResponse.Error("landing_unreachable", "The landing store is not reachable."),
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var landed = await repository.CountAsync(cancellationToken);
        return Results.Ok(ApiResponse.Ok(new ReadyResponse("ready", landed)));
    }
}

public sealed record ReadyResponse(string Status, long LandedEvents);
