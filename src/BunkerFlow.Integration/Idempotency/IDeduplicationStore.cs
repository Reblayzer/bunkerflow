namespace BunkerFlow.Integration.Idempotency;

/// <summary>
/// Backs the idempotent-consumer pattern. Source systems replay, Kafka
/// redelivers on rebalance and Service Bus is at-least-once, so the same trade
/// will arrive more than once and must only land once.
///
/// Reserve-then-release rather than a plain "mark seen": a key is claimed
/// before publishing and released again if publishing fails permanently. A
/// record that never made it downstream stays eligible for reprocessing
/// instead of being silently swallowed.
/// </summary>
public interface IDeduplicationStore
{
    /// <returns>
    /// True when this call claimed the key, false when someone already had it.
    /// The claim is atomic, so two concurrent consumers cannot both win.
    /// </returns>
    Task<bool> TryReserveAsync(string deduplicationKey, CancellationToken cancellationToken);

    /// <summary>Gives a claimed key back after a failed publish.</summary>
    Task ReleaseAsync(string deduplicationKey, CancellationToken cancellationToken);
}
