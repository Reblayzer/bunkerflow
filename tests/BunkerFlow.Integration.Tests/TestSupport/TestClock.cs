namespace BunkerFlow.Integration.Tests.TestSupport;

/// <summary>
/// A TimeProvider the tests control, so anything stamped with a clock is
/// asserted against a fixed value instead of "roughly now".
/// </summary>
public sealed class TestClock : TimeProvider
{
    public static readonly DateTimeOffset DefaultNow =
        new(2026, 8, 2, 10, 0, 0, TimeSpan.Zero);

    private DateTimeOffset _now;

    public TestClock(DateTimeOffset? now = null) => _now = now ?? DefaultNow;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now = _now.Add(by);
}
