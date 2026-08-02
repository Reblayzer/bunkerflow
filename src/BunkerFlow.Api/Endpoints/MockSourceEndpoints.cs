using System.Globalization;

namespace BunkerFlow.Api.Endpoints;

/// <summary>
/// Stands in for the business systems BunkerFlow would integrate with. This is
/// simulated data, deliberately including the awkward shapes real exports have:
/// different field names per system, comma decimals, and the occasional record
/// that fails validation.
///
/// The batch worker polls this endpoint exactly as it would poll a real one.
/// </summary>
public static class MockSourceEndpoints
{
    private static readonly string[] Ports = ["DKFRC", "SGSIN", "NLRTM", "AEFJR", "USHOU"];
    private static readonly string[] Products = ["VLSFO", "HSFO", "LSMGO", "MGO"];
    private static readonly string[] Counterparties =
        ["Bunker Holding A/S", "Unit Bunkering ApS", "Nordic Marine Fuels", "Delta Energy DMCC"];

    /// <summary>
    /// IMO numbers with correct check digits. MockSourceEndpointTests asserts
    /// this, so a typo here fails the build instead of quietly inflating the
    /// rejection rate in a demo.
    /// </summary>
    public static readonly string[] Vessels =
        ["9074729", "9241061", "9321483", "9454125", "9632959"];

    /// <summary>Deliberately broken check digit, used to exercise dead-lettering.</summary>
    public const string InvalidVesselImo = "9074720";

    public static void MapMockSourceEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/mock-sources").WithTags("Mock sources");

        group.MapGet("/trading-desk/trades", (int count = 10, int seed = 0) =>
                Results.Ok(GenerateTradingDeskRecords(count, seed)))
            .WithName("MockTradingDeskTrades")
            .WithSummary("Simulated trading desk export, camelCase field names.");

        group.MapGet("/erp/trades", (int count = 10, int seed = 0) =>
                Results.Ok(GenerateErpRecords(count, seed)))
            .WithName("MockErpTrades")
            .WithSummary("Simulated ERP export, snake_case names and comma decimals.");
    }

    private static List<Dictionary<string, string?>> GenerateTradingDeskRecords(int count, int seed)
    {
        var random = new Random(seed == 0 ? 20260802 : seed);

        return Enumerable.Range(1, Math.Clamp(count, 1, 200)).Select(index =>
        {
            var tradedAt = new DateTimeOffset(2026, 8, 1, 6, 0, 0, TimeSpan.Zero)
                .AddMinutes(random.Next(0, 900));

            return new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["sourceRecordId"] = $"TD-{seed}-{index}",
                ["tradeReference"] = $"TR-{100000 + index}",
                ["vesselImo"] = Vessels[random.Next(Vessels.Length)],
                ["port"] = Ports[random.Next(Ports.Length)],
                ["product"] = Products[random.Next(Products.Length)],
                ["quantityMt"] = (300 + random.Next(0, 1500)).ToString(CultureInfo.InvariantCulture),
                ["priceUsdPerMt"] = (450 + random.Next(0, 250)).ToString(CultureInfo.InvariantCulture),
                ["counterparty"] = Counterparties[random.Next(Counterparties.Length)],
                ["tradedAtUtc"] = tradedAt.ToString("O", CultureInfo.InvariantCulture),
            };
        }).ToList();
    }

    private static List<Dictionary<string, string?>> GenerateErpRecords(int count, int seed)
    {
        var random = new Random(seed == 0 ? 20260803 : seed);

        return Enumerable.Range(1, Math.Clamp(count, 1, 200)).Select(index =>
        {
            var tradedAt = new DateTimeOffset(2026, 8, 1, 5, 0, 0, TimeSpan.Zero)
                .AddMinutes(random.Next(0, 900));

            // Every seventh record carries a broken IMO check digit, so the
            // dead-letter path is visible in a demo instead of theoretical.
            var imo = index % 7 == 0 ? InvalidVesselImo : Vessels[random.Next(Vessels.Length)];

            return new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["sourceRecordId"] = $"ERP-{seed}-{index}",
                ["deal_id"] = $"D-{500000 + index}",
                ["imo"] = imo,
                ["delivery_port"] = Ports[random.Next(Ports.Length)],
                ["fuel_grade"] = Products[random.Next(Products.Length)],
                ["volume_mt"] = $"{300 + random.Next(0, 1500)},{random.Next(0, 99):D2}",
                ["unit_price"] = $"{450 + random.Next(0, 250)},{random.Next(0, 99):D2}",
                ["supplier"] = Counterparties[random.Next(Counterparties.Length)],
                ["trade_date"] = tradedAt.ToString("O", CultureInfo.InvariantCulture),
            };
        }).ToList();
    }
}
