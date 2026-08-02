using System.Text.RegularExpressions;
using BunkerFlow.Contracts;

namespace BunkerFlow.Integration.Validation;

/// <summary>
/// Data-quality rules for a bunker trade. These are the checks that stop
/// plausible-looking but wrong data from landing in the platform: a mistyped
/// IMO number, a price of zero, a trade dated next year.
/// </summary>
public sealed partial class BunkerTradeValidator : IEventValidator
{
    /// <summary>Grades the platform currently accepts.</summary>
    public static readonly IReadOnlySet<string> KnownProducts =
        new HashSet<string>(StringComparer.Ordinal) { "VLSFO", "HSFO", "LSMGO", "MGO", "ULSFO", "LNG" };

    /// <summary>Largest single stem we treat as plausible rather than a unit error.</summary>
    public const decimal MaxQuantityMt = 20_000m;

    public const decimal MaxPriceUsdPerMt = 5_000m;

    /// <summary>Clock skew we tolerate on a source system's timestamp.</summary>
    public static readonly TimeSpan FutureTolerance = TimeSpan.FromMinutes(5);

    private readonly TimeProvider _timeProvider;

    public BunkerTradeValidator(TimeProvider timeProvider) => _timeProvider = timeProvider;

    public ValidationResult Validate(IntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var failures = new List<ValidationFailure>();
        var trade = integrationEvent.Payload;

        if (integrationEvent.SchemaVersion != IntegrationEvent.CurrentSchemaVersion)
        {
            failures.Add(new ValidationFailure(
                "schemaVersion",
                "unsupported_schema_version",
                $"Expected schema version {IntegrationEvent.CurrentSchemaVersion}, got {integrationEvent.SchemaVersion}."));
        }

        if (string.IsNullOrWhiteSpace(trade.TradeReference))
        {
            failures.Add(new ValidationFailure("tradeReference", "required", "Trade reference is empty."));
        }

        if (!IsValidImo(trade.VesselImo))
        {
            failures.Add(new ValidationFailure(
                "vesselImo",
                "invalid_imo",
                $"'{trade.VesselImo}' is not a valid IMO number (7 digits, last one a check digit)."));
        }

        if (!LocodePattern().IsMatch(trade.Port))
        {
            failures.Add(new ValidationFailure(
                "port",
                "invalid_locode",
                $"'{trade.Port}' is not a UN/LOCODE (5 letters, for example DKFRC)."));
        }

        if (!KnownProducts.Contains(trade.Product))
        {
            failures.Add(new ValidationFailure(
                "product",
                "unknown_product",
                $"'{trade.Product}' is not one of {string.Join(", ", KnownProducts.Order())}."));
        }

        if (trade.QuantityMt <= 0 || trade.QuantityMt > MaxQuantityMt)
        {
            failures.Add(new ValidationFailure(
                "quantityMt",
                "out_of_range",
                $"Quantity must be greater than 0 and at most {MaxQuantityMt} MT, got {trade.QuantityMt}."));
        }

        if (trade.PriceUsdPerMt <= 0 || trade.PriceUsdPerMt > MaxPriceUsdPerMt)
        {
            failures.Add(new ValidationFailure(
                "priceUsdPerMt",
                "out_of_range",
                $"Price must be greater than 0 and at most {MaxPriceUsdPerMt} USD/MT, got {trade.PriceUsdPerMt}."));
        }

        if (string.IsNullOrWhiteSpace(trade.Counterparty))
        {
            failures.Add(new ValidationFailure("counterparty", "required", "Counterparty is empty."));
        }

        var latestAcceptable = _timeProvider.GetUtcNow().Add(FutureTolerance);
        if (trade.TradedAtUtc > latestAcceptable)
        {
            failures.Add(new ValidationFailure(
                "tradedAtUtc",
                "future_timestamp",
                $"Trade timestamp {trade.TradedAtUtc:O} is more than {FutureTolerance.TotalMinutes} minutes ahead of now."));
        }

        return failures.Count == 0 ? ValidationResult.Valid() : ValidationResult.Invalid(failures);
    }

    /// <summary>
    /// IMO numbers carry a check digit: multiply the first six digits by 7, 6,
    /// 5, 4, 3, 2 and the last digit of that sum must equal the seventh digit.
    /// It catches transposed digits, which is the common typo.
    /// </summary>
    public static bool IsValidImo(string? imo)
    {
        if (imo is null || imo.Length != 7 || !imo.All(char.IsAsciiDigit))
        {
            return false;
        }

        var checksum = 0;
        for (var position = 0; position < 6; position++)
        {
            checksum += (imo[position] - '0') * (7 - position);
        }

        return checksum % 10 == imo[6] - '0';
    }

    [GeneratedRegex("^[A-Z]{5}$")]
    private static partial Regex LocodePattern();
}
