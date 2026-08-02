using System.Globalization;
using BunkerFlow.Contracts;
using BunkerFlow.Integration.Errors;

namespace BunkerFlow.Integration.Normalization;

/// <summary>
/// Maps bunker trade records from any source system onto <see cref="BunkerTrade"/>.
///
/// Source systems disagree about field names (a trading desk exports
/// "deal_id", the ERP exports "TradeRef"), so each target field is resolved
/// through a list of accepted aliases. Adding a new source system is usually
/// an alias entry, not a new code path.
/// </summary>
public sealed class BunkerTradeNormalizer : IRecordNormalizer
{
    private static readonly string[] TradeReferenceAliases =
        ["tradeReference", "trade_reference", "trade_ref", "TradeRef", "deal_id", "dealId"];

    private static readonly string[] VesselAliases =
        ["vesselImo", "vessel_imo", "imo", "IMO", "vessel"];

    private static readonly string[] PortAliases =
        ["port", "portCode", "port_code", "locode", "unlocode", "delivery_port"];

    private static readonly string[] ProductAliases =
        ["product", "grade", "fuel_grade", "fuelGrade", "productCode"];

    private static readonly string[] QuantityAliases =
        ["quantityMt", "quantity_mt", "quantity", "qty", "volume_mt", "volumeMt"];

    private static readonly string[] PriceAliases =
        ["priceUsdPerMt", "price_usd_per_mt", "price", "unit_price", "unitPrice"];

    private static readonly string[] CounterpartyAliases =
        ["counterparty", "counter_party", "supplier", "seller"];

    private static readonly string[] TradedAtAliases =
        ["tradedAtUtc", "traded_at_utc", "tradedAt", "traded_at", "timestamp", "trade_date"];

    private readonly TimeProvider _timeProvider;

    public BunkerTradeNormalizer(TimeProvider timeProvider) => _timeProvider = timeProvider;

    public bool CanNormalize(SourceRecord record) =>
        Resolve(record, TradeReferenceAliases) is not null;

    public IntegrationEvent Normalize(SourceRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var tradedAtUtc = ParseTimestamp(record, TradedAtAliases);

        var trade = new BunkerTrade
        {
            TradeReference = Require(record, TradeReferenceAliases, "tradeReference"),
            VesselImo = Require(record, VesselAliases, "vesselImo").Trim().ToUpperInvariant()
                .Replace("IMO", string.Empty, StringComparison.Ordinal).Trim(),
            Port = Require(record, PortAliases, "port").Trim().ToUpperInvariant(),
            Product = Require(record, ProductAliases, "product").Trim().ToUpperInvariant(),
            QuantityMt = ParseDecimal(record, QuantityAliases, "quantityMt"),
            PriceUsdPerMt = ParseDecimal(record, PriceAliases, "priceUsdPerMt"),
            Counterparty = Require(record, CounterpartyAliases, "counterparty").Trim(),
            TradedAtUtc = tradedAtUtc,
        };

        return new IntegrationEvent
        {
            EventId = IntegrationEvent.BuildEventId(record.SourceSystem, record.SourceRecordId),
            EventType = IntegrationEvent.BunkerTradeRecorded,
            SchemaVersion = IntegrationEvent.CurrentSchemaVersion,
            SourceSystem = record.SourceSystem,
            SourceRecordId = record.SourceRecordId,
            Channel = record.Channel,
            OccurredAtUtc = tradedAtUtc,
            IngestedAtUtc = _timeProvider.GetUtcNow(),
            Payload = trade,
        };
    }

    private static string? Resolve(SourceRecord record, string[] aliases)
    {
        foreach (var alias in aliases)
        {
            var value = record.Field(alias);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string Require(SourceRecord record, string[] aliases, string targetField)
    {
        return Resolve(record, aliases)
            ?? throw new NormalizationException(
                $"Record '{record.SourceRecordId}' from '{record.SourceSystem}' is missing " +
                $"required field '{targetField}' (accepted aliases: {string.Join(", ", aliases)}).");
    }

    private static decimal ParseDecimal(SourceRecord record, string[] aliases, string targetField)
    {
        var raw = Require(record, aliases, targetField);

        // Source systems send both "1234.5" and the European "1234,5".
        var cleaned = raw.Trim().Replace(" ", string.Empty, StringComparison.Ordinal);
        if (cleaned.Contains(',', StringComparison.Ordinal)
            && !cleaned.Contains('.', StringComparison.Ordinal))
        {
            cleaned = cleaned.Replace(',', '.');
        }

        if (!decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
        {
            throw new NormalizationException(
                $"Field '{targetField}' on record '{record.SourceRecordId}' is not a number: '{raw}'.");
        }

        return value;
    }

    private static DateTimeOffset ParseTimestamp(SourceRecord record, string[] aliases)
    {
        var raw = Require(record, aliases, "tradedAtUtc");

        if (!DateTimeOffset.TryParse(
                raw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out var parsed))
        {
            throw new NormalizationException(
                $"Field 'tradedAtUtc' on record '{record.SourceRecordId}' is not a timestamp: '{raw}'.");
        }

        return parsed.ToUniversalTime();
    }
}
