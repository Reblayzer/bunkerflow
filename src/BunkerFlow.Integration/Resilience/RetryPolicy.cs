using BunkerFlow.Integration.Errors;

namespace BunkerFlow.Integration.Resilience;

/// <summary>
/// Exponential backoff with jitter, applied only to errors marked transient.
/// A permanent error fails on the first attempt instead of burning the retry
/// budget on something that cannot succeed.
/// </summary>
public sealed class RetryPolicy
{
    private readonly int _maxAttempts;
    private readonly TimeSpan _baseDelay;
    private readonly TimeSpan _maxDelay;
    private readonly TimeProvider _timeProvider;
    private readonly Func<double> _jitter;

    public RetryPolicy(
        int maxAttempts,
        TimeSpan baseDelay,
        TimeSpan maxDelay,
        TimeProvider timeProvider,
        Func<double>? jitter = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);

        _maxAttempts = maxAttempts;
        _baseDelay = baseDelay;
        _maxDelay = maxDelay;
        _timeProvider = timeProvider;
        _jitter = jitter ?? (() => Random.Shared.NextDouble());
    }

    /// <summary>A sensible default for broker publishes.</summary>
    public static RetryPolicy Default(TimeProvider timeProvider) =>
        new(maxAttempts: 4,
            baseDelay: TimeSpan.FromMilliseconds(200),
            maxDelay: TimeSpan.FromSeconds(5),
            timeProvider);

    /// <returns>The number of attempts that were made.</returns>
    public async Task<int> ExecuteAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await action(cancellationToken).ConfigureAwait(false);
                return attempt;
            }
            catch (TransientPublishException) when (attempt < _maxAttempts)
            {
                await Task.Delay(DelayFor(attempt), _timeProvider, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    /// <summary>Full jitter: a random point between zero and the capped exponential delay.</summary>
    internal TimeSpan DelayFor(int attempt)
    {
        var exponential = _baseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1);
        var capped = Math.Min(exponential, _maxDelay.TotalMilliseconds);
        return TimeSpan.FromMilliseconds(capped * _jitter());
    }
}
