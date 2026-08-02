using BunkerFlow.Contracts;
using BunkerFlow.Integration.Landing;
using BunkerFlow.Integration.Normalization;
using BunkerFlow.Integration.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Parquet.Serialization;

namespace BunkerFlow.Integration.Tests;

/// <summary>
/// Writing a file that no reader can open would be a silent failure: the
/// pipeline reports success and the lakehouse gets nothing usable. These tests
/// read the Parquet back.
/// </summary>
public sealed class ParquetLandingWriterTests : IDisposable
{
    private readonly TestClock _clock = new();
    private readonly BunkerTradeNormalizer _normalizer;
    private readonly string _root;

    public ParquetLandingWriterTests()
    {
        _normalizer = new BunkerTradeNormalizer(_clock);
        _root = Path.Combine(Path.GetTempPath(), $"bunkerflow-parquet-{Guid.NewGuid():N}");
    }

    [Fact]
    public async Task Should_write_a_file_that_reads_back_with_the_same_values()
    {
        var landed = _normalizer.Normalize(RecordBuilder.Valid().From("erp", "P-1").Build());

        await Writer().WriteAsync([landed], CancellationToken.None);

        var rows = await ReadAllRowsAsync();
        var row = Assert.Single(rows);

        Assert.Equal(landed.EventId, row.EventId);
        Assert.Equal(landed.SourceSystem, row.SourceSystem);
        Assert.Equal("DKFRC", row.Port);
        Assert.Equal(850.5m, row.QuantityMt);
        Assert.Equal(612.25m, row.PriceUsdPerMt);
        Assert.Equal(landed.Payload.TotalUsd, row.TotalUsd);
        Assert.Equal(landed.OccurredAtUtc.UtcDateTime, row.OccurredAtUtc);
    }

    [Fact]
    public async Task Should_partition_by_trade_date()
    {
        var first = _normalizer.Normalize(
            RecordBuilder.Valid().From("erp", "P-1").With("tradedAtUtc", "2026-07-30T10:00:00Z").Build());
        var second = _normalizer.Normalize(
            RecordBuilder.Valid().From("erp", "P-2").With("tradedAtUtc", "2026-07-31T10:00:00Z").Build());

        await Writer().WriteAsync([first, second], CancellationToken.None);

        Assert.True(Directory.Exists(Path.Combine(_root, "dt=2026-07-30")));
        Assert.True(Directory.Exists(Path.Combine(_root, "dt=2026-07-31")));
        Assert.Equal(2, Directory.GetFiles(_root, "*.parquet", SearchOption.AllDirectories).Length);
    }

    [Fact]
    public async Task Should_write_every_event_in_a_batch()
    {
        var events = Enumerable.Range(1, 25)
            .Select(index => _normalizer.Normalize(
                RecordBuilder.Valid().From("erp", $"P-{index}").Build()))
            .ToList();

        await Writer().WriteAsync(events, CancellationToken.None);

        var rows = await ReadAllRowsAsync();

        Assert.Equal(25, rows.Count);
        Assert.Equal(25, rows.Select(row => row.EventId).Distinct().Count());
    }

    [Fact]
    public async Task Should_write_nothing_for_an_empty_batch()
    {
        await Writer().WriteAsync([], CancellationToken.None);

        Assert.False(Directory.Exists(_root));
    }

    private ParquetLandingWriter Writer() =>
        new(_root, _clock, NullLogger<ParquetLandingWriter>.Instance);

    private async Task<List<ParquetLandingWriter.TradeRow>> ReadAllRowsAsync()
    {
        var rows = new List<ParquetLandingWriter.TradeRow>();

        foreach (var file in Directory.GetFiles(_root, "*.parquet", SearchOption.AllDirectories))
        {
            await using var stream = File.OpenRead(file);
            var result = await ParquetSerializer.DeserializeAsync<ParquetLandingWriter.TradeRow>(stream);
            rows.AddRange(result.Data);
        }

        return rows;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
