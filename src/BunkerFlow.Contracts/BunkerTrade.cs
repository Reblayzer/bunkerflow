namespace BunkerFlow.Contracts;

/// <summary>
/// The normalized business payload carried by an integration event: a single
/// bunker fuel trade, expressed the same way no matter which source system it
/// came from.
/// </summary>
public sealed record BunkerTrade
{
    public required string TradeReference { get; init; }

    /// <summary>IMO number of the vessel being bunkered (7 digits).</summary>
    public required string VesselImo { get; init; }

    /// <summary>UN/LOCODE of the delivery port, for example DKFRC.</summary>
    public required string Port { get; init; }

    /// <summary>Fuel grade, for example VLSFO, HSFO or MGO.</summary>
    public required string Product { get; init; }

    public required decimal QuantityMt { get; init; }

    public required decimal PriceUsdPerMt { get; init; }

    public required string Counterparty { get; init; }

    public required DateTimeOffset TradedAtUtc { get; init; }

    public decimal TotalUsd => decimal.Round(QuantityMt * PriceUsdPerMt, 2);
}
