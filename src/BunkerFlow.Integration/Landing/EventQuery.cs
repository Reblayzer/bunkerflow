namespace BunkerFlow.Integration.Landing;

/// <summary>
/// Filter for the query endpoint. Always paginated: the landing store is
/// append-only and grows without bound, so an unbounded read is never offered.
/// </summary>
public sealed record EventQuery
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 500;

    public string? SourceSystem { get; init; }

    public string? Port { get; init; }

    public string? Product { get; init; }

    public DateTimeOffset? FromUtc { get; init; }

    public DateTimeOffset? ToUtc { get; init; }

    public int Limit { get; init; } = DefaultLimit;

    public int Offset { get; init; }

    /// <summary>Clamps caller-supplied paging into the allowed range.</summary>
    public EventQuery Normalized() => this with
    {
        Limit = Math.Clamp(Limit <= 0 ? DefaultLimit : Limit, 1, MaxLimit),
        Offset = Math.Max(0, Offset),
    };
}
