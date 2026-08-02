using BunkerFlow.Api.Security;
using BunkerFlow.Integration.Landing;

namespace BunkerFlow.Api.Endpoints;

/// <summary>Read side: what actually landed in the platform.</summary>
public static class EventEndpoints
{
    public static void MapEventEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/events", QueryAsync)
            .WithTags("Events")
            .WithName("QueryEvents")
            .WithSummary("Query landed events. Always paginated.")
            .AddEndpointFilter<ApiKeyEndpointFilter>();
    }

    private static async Task<IResult> QueryAsync(
        IEventRepository repository,
        CancellationToken cancellationToken,
        string? sourceSystem = null,
        string? port = null,
        string? product = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int limit = EventQuery.DefaultLimit,
        int offset = 0)
    {
        if (from is not null && to is not null && from > to)
        {
            return Results.BadRequest(ApiResponse.Error(
                "invalid_range", "'from' must not be later than 'to'."));
        }

        var query = new EventQuery
        {
            SourceSystem = sourceSystem,
            Port = port,
            Product = product,
            FromUtc = from,
            ToUtc = to,
            Limit = limit,
            Offset = offset,
        }.Normalized();

        var events = await repository.QueryAsync(query, cancellationToken);

        return Results.Ok(ApiResponse.Ok(new EventPage(
            Items: events,
            Count: events.Count,
            Limit: query.Limit,
            Offset: query.Offset)));
    }
}

public sealed record EventPage(
    IReadOnlyList<BunkerFlow.Contracts.IntegrationEvent> Items,
    int Count,
    int Limit,
    int Offset);
