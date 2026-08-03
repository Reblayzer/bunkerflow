#!/usr/bin/env bash
# Recomputes, from the committed sample Parquet, the same aggregates the
# Databricks notebook produces. Used to check the notebook output and to write
# the numbers into the README without transcribing them by hand.
set -euo pipefail
repo="$(cd "$(dirname "$0")/.." && pwd)"
source "$repo/scripts/env.sh"

work=/tmp/parquet-aggregate
rm -rf "$work"
mkdir -p "$work"
cd "$work"

dotnet new console -n Aggregate --framework net10.0 >/dev/null
cd Aggregate
dotnet add package Parquet.Net --version 6.0.3 >/dev/null

cat > Program.cs <<'CS'
using Parquet.Serialization;

var files = Directory.GetFiles("SAMPLE_ROOT", "*.parquet", SearchOption.AllDirectories);

var rows = new List<Row>();
foreach (var file in files)
{
    await using var stream = File.OpenRead(file);
    var result = await ParquetSerializer.DeserializeAsync<Row>(stream);
    rows.AddRange(result.Data);
}

// Silver: dedupe on the derived event id, keeping the newest ingest.
var silver = rows
    .GroupBy(r => r.EventId)
    .Select(g => g.OrderByDescending(r => r.IngestedAtUtc).First())
    .ToList();

Console.WriteLine($"bronze rows {rows.Count} -> silver rows {silver.Count}");

Console.WriteLine();
Console.WriteLine("channel | source_system | trades | total_mt");
foreach (var group in silver
             .GroupBy(r => (r.Channel, r.SourceSystem))
             .OrderByDescending(g => g.Count()))
{
    Console.WriteLine(
        $"{group.Key.Channel,-7} | {group.Key.SourceSystem,-14} | {group.Count(),6} | " +
        $"{Round(group.Sum(r => r.QuantityMt), 1),9}");
}

Console.WriteLine();
Console.WriteLine("port  | product | trades | total_mt | total_usd | avg_price | vessels");
foreach (var group in silver
             .GroupBy(r => (r.Port, r.Product))
             .OrderByDescending(g => g.Sum(r => r.TotalUsd)))
{
    Console.WriteLine(
        $"{group.Key.Port,-5} | {group.Key.Product,-7} | {group.Count(),6} | " +
        $"{Round(group.Sum(r => r.QuantityMt), 1),8} | " +
        $"{Round(group.Sum(r => r.TotalUsd), 2),9} | " +
        $"{Round(group.Average(r => r.PriceUsdPerMt), 2),9} | " +
        $"{group.Select(r => r.VesselImo).Distinct().Count(),7}");
}

// Spark's round() is half-up. C#'s Math.Round defaults to banker's rounding,
// which disagrees on exact midpoints, so this script would report a figure the
// notebook never produced.
static decimal Round(decimal value, int decimals) =>
    Math.Round(value, decimals, MidpointRounding.AwayFromZero);

class Row
{
    public string EventId { get; set; } = "";
    public string Channel { get; set; } = "";
    public string SourceSystem { get; set; } = "";
    public string Port { get; set; } = "";
    public string Product { get; set; } = "";
    public string VesselImo { get; set; } = "";
    public decimal QuantityMt { get; set; }
    public decimal PriceUsdPerMt { get; set; }
    public decimal TotalUsd { get; set; }
    public DateTime IngestedAtUtc { get; set; }
}
CS

sed -i "s|SAMPLE_ROOT|$repo/samples/landing|" Program.cs
dotnet run --project . 2>&1 | tail -40
