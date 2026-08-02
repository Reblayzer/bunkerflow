using BunkerFlow.Integration.Errors;
using BunkerFlow.Integration.Resilience;

namespace BunkerFlow.Integration.Tests;

public sealed class RetryPolicyTests
{
    // Zero jitter and a tiny base delay keep these tests instant and deterministic.
    private readonly RetryPolicy _policy = new(
        maxAttempts: 3,
        baseDelay: TimeSpan.FromMilliseconds(1),
        maxDelay: TimeSpan.FromMilliseconds(10),
        TimeProvider.System,
        jitter: () => 0);

    [Fact]
    public async Task Should_not_retry_when_the_action_succeeds()
    {
        var calls = 0;

        var attempts = await _policy.ExecuteAsync(_ =>
        {
            calls++;
            return Task.CompletedTask;
        }, CancellationToken.None);

        Assert.Equal(1, calls);
        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task Should_retry_a_transient_failure_until_it_succeeds()
    {
        var calls = 0;

        var attempts = await _policy.ExecuteAsync(_ =>
        {
            calls++;
            if (calls < 3)
            {
                throw new TransientPublishException("broker timed out");
            }

            return Task.CompletedTask;
        }, CancellationToken.None);

        Assert.Equal(3, calls);
        Assert.Equal(3, attempts);
    }

    [Fact]
    public async Task Should_give_up_after_the_attempt_budget_is_spent()
    {
        var calls = 0;

        await Assert.ThrowsAsync<TransientPublishException>(() =>
            _policy.ExecuteAsync(_ =>
            {
                calls++;
                throw new TransientPublishException("still down");
            }, CancellationToken.None));

        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task Should_not_retry_an_error_that_retrying_cannot_fix()
    {
        var calls = 0;

        await Assert.ThrowsAsync<PermanentPublishException>(() =>
            _policy.ExecuteAsync(_ =>
            {
                calls++;
                throw new PermanentPublishException("topic does not exist");
            }, CancellationToken.None));

        Assert.Equal(1, calls);
    }

    [Fact]
    public void Should_back_off_exponentially_and_stop_at_the_ceiling()
    {
        var policy = new RetryPolicy(
            maxAttempts: 10,
            baseDelay: TimeSpan.FromMilliseconds(100),
            maxDelay: TimeSpan.FromMilliseconds(500),
            TimeProvider.System,
            jitter: () => 1);

        Assert.Equal(TimeSpan.FromMilliseconds(100), policy.DelayFor(1));
        Assert.Equal(TimeSpan.FromMilliseconds(200), policy.DelayFor(2));
        Assert.Equal(TimeSpan.FromMilliseconds(400), policy.DelayFor(3));
        Assert.Equal(TimeSpan.FromMilliseconds(500), policy.DelayFor(4));
        Assert.Equal(TimeSpan.FromMilliseconds(500), policy.DelayFor(9));
    }

    [Fact]
    public void Should_keep_jittered_delays_inside_the_computed_window()
    {
        var policy = new RetryPolicy(
            maxAttempts: 5,
            baseDelay: TimeSpan.FromMilliseconds(100),
            maxDelay: TimeSpan.FromSeconds(1),
            TimeProvider.System,
            jitter: () => 0.5);

        Assert.Equal(TimeSpan.FromMilliseconds(50), policy.DelayFor(1));
        Assert.Equal(TimeSpan.FromMilliseconds(100), policy.DelayFor(2));
    }

    [Fact]
    public async Task Should_stop_when_the_caller_cancels()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _policy.ExecuteAsync(token =>
            {
                token.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }, cancellation.Token));
    }
}
