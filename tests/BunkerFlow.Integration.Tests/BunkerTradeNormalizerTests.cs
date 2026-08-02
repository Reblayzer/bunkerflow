using BunkerFlow.Contracts;
using BunkerFlow.Integration.Errors;
using BunkerFlow.Integration.Normalization;
using BunkerFlow.Integration.Tests.TestSupport;

namespace BunkerFlow.Integration.Tests;

public sealed class BunkerTradeNormalizerTests
{
    private readonly TestClock _clock = new();
    private readonly BunkerTradeNormalizer _normalizer;

    public BunkerTradeNormalizerTests() => _normalizer = new BunkerTradeNormalizer(_clock);

    [Fact]
    public void Should_map_a_well_formed_record_onto_the_common_contract()
    {
        var result = _normalizer.Normalize(RecordBuilder.Valid().Build());

        Assert.Equal(IntegrationEvent.BunkerTradeRecorded, result.EventType);
        Assert.Equal(IntegrationEvent.CurrentSchemaVersion, result.SchemaVersion);
        Assert.Equal("trading-desk", result.SourceSystem);
        Assert.Equal("TR-100045", result.Payload.TradeReference);
        Assert.Equal(RecordBuilder.ValidImo, result.Payload.VesselImo);
        Assert.Equal(850.5m, result.Payload.QuantityMt);
        Assert.Equal(612.25m, result.Payload.PriceUsdPerMt);
        Assert.Equal(TestClock.DefaultNow, result.IngestedAtUtc);
    }

    [Fact]
    public void Should_accept_a_source_system_that_uses_its_own_field_names()
    {
        var record = RecordBuilder.Valid()
            .WithFieldsOnly(new Dictionary<string, string?>
            {
                ["deal_id"] = "ERP-778",
                ["imo"] = RecordBuilder.ValidImo,
                ["delivery_port"] = "dkfrc",
                ["fuel_grade"] = "vlsfo",
                ["volume_mt"] = "1200",
                ["unit_price"] = "590",
                ["supplier"] = "Unit Bunkering ApS",
                ["trade_date"] = "2026-07-31T14:00:00Z",
            })
            .Build();

        var result = _normalizer.Normalize(record);

        Assert.Equal("ERP-778", result.Payload.TradeReference);
        Assert.Equal("DKFRC", result.Payload.Port);
        Assert.Equal("VLSFO", result.Payload.Product);
        Assert.Equal(1200m, result.Payload.QuantityMt);
    }

    [Fact]
    public void Should_parse_a_decimal_comma_from_a_european_export()
    {
        var record = RecordBuilder.Valid().With("quantityMt", "1 250,75").Build();

        var result = _normalizer.Normalize(record);

        Assert.Equal(1250.75m, result.Payload.QuantityMt);
    }

    [Fact]
    public void Should_strip_the_IMO_prefix_some_systems_include()
    {
        var record = RecordBuilder.Valid().With("vesselImo", "IMO 9074729").Build();

        var result = _normalizer.Normalize(record);

        Assert.Equal("9074729", result.Payload.VesselImo);
    }

    [Fact]
    public void Should_normalize_the_timestamp_to_UTC()
    {
        var record = RecordBuilder.Valid().With("tradedAtUtc", "2026-08-01T10:30:00+02:00").Build();

        var result = _normalizer.Normalize(record);

        Assert.Equal(TimeSpan.Zero, result.Payload.TradedAtUtc.Offset);
        Assert.Equal(new DateTimeOffset(2026, 8, 1, 8, 30, 0, TimeSpan.Zero), result.Payload.TradedAtUtc);
    }

    [Fact]
    public void Should_reject_a_record_that_is_missing_a_required_field()
    {
        var record = RecordBuilder.Valid().Without("counterparty").Build();

        var exception = Assert.Throws<NormalizationException>(() => _normalizer.Normalize(record));

        Assert.Contains("counterparty", exception.Message, StringComparison.Ordinal);
        Assert.Equal("normalization_failed", exception.Code);
    }

    [Fact]
    public void Should_reject_a_quantity_that_is_not_a_number()
    {
        var record = RecordBuilder.Valid().With("quantityMt", "eight hundred").Build();

        Assert.Throws<NormalizationException>(() => _normalizer.Normalize(record));
    }

    [Fact]
    public void Should_derive_the_same_event_id_when_a_source_replays_a_record()
    {
        var record = RecordBuilder.Valid().From("trading-desk", "TD-42").Build();

        var first = _normalizer.Normalize(record);
        var second = _normalizer.Normalize(record);

        Assert.Equal(first.EventId, second.EventId);
        Assert.Equal("trading-desk:TD-42", first.DeduplicationKey);
    }

    [Fact]
    public void Should_derive_different_event_ids_for_different_source_systems()
    {
        var fromDesk = _normalizer.Normalize(RecordBuilder.Valid().From("trading-desk", "1").Build());
        var fromErp = _normalizer.Normalize(RecordBuilder.Valid().From("erp", "1").Build());

        Assert.NotEqual(fromDesk.EventId, fromErp.EventId);
    }
}
