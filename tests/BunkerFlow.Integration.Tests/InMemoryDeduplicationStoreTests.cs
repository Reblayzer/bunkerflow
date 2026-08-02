using BunkerFlow.Integration.Idempotency;

namespace BunkerFlow.Integration.Tests;

public sealed class InMemoryDeduplicationStoreTests
{
    private readonly InMemoryDeduplicationStore _store = new();

    [Fact]
    public async Task Should_let_the_first_caller_claim_a_key()
    {
        Assert.True(await _store.TryReserveAsync("trading-desk:1", CancellationToken.None));
    }

    [Fact]
    public async Task Should_refuse_a_key_that_is_already_claimed()
    {
        await _store.TryReserveAsync("trading-desk:1", CancellationToken.None);

        Assert.False(await _store.TryReserveAsync("trading-desk:1", CancellationToken.None));
    }

    [Fact]
    public async Task Should_treat_the_same_id_from_two_source_systems_as_different_records()
    {
        Assert.True(await _store.TryReserveAsync("trading-desk:1", CancellationToken.None));
        Assert.True(await _store.TryReserveAsync("erp:1", CancellationToken.None));
    }

    [Fact]
    public async Task Should_make_a_released_key_claimable_again()
    {
        await _store.TryReserveAsync("trading-desk:1", CancellationToken.None);
        await _store.ReleaseAsync("trading-desk:1", CancellationToken.None);

        Assert.True(await _store.TryReserveAsync("trading-desk:1", CancellationToken.None));
    }

    [Fact]
    public async Task Should_let_exactly_one_of_many_concurrent_callers_win()
    {
        var attempts = Enumerable
            .Range(0, 50)
            .Select(_ => _store.TryReserveAsync("trading-desk:contended", CancellationToken.None));

        var results = await Task.WhenAll(attempts);

        Assert.Equal(1, results.Count(reserved => reserved));
    }
}
