#!/usr/bin/env bash
# Prints the schema of the sample Parquet, so the Databricks notebook is
# written against the real column names rather than assumed ones.
# Uses a throwaway console project outside the repo.
set -euo pipefail
repo="$(cd "$(dirname "$0")/.." && pwd)"
source "$repo/scripts/env.sh"

work=/tmp/parquet-inspect
rm -rf "$work"
mkdir -p "$work"
cd "$work"

dotnet new console -n Inspect --framework net10.0 >/dev/null
cd Inspect
dotnet add package Parquet.Net --version 6.0.3 >/dev/null

cat > Program.cs <<'CS'
using Parquet;
using Parquet.Serialization;

var files = Directory.GetFiles("SAMPLE_ROOT", "*.parquet", SearchOption.AllDirectories);
Console.WriteLine($"files: {files.Length}");

await using (var stream = File.OpenRead(files[0]))
await using (var reader = await ParquetReader.CreateAsync(stream))
{
    Console.WriteLine("--- schema ---");
    foreach (var field in reader.Schema.DataFields)
    {
        Console.WriteLine($"{field.Name,-20} {field.ClrType.Name} nullable={field.IsNullable}");
    }
}

var rows = new List<Row>();
foreach (var file in files)
{
    await using var stream = File.OpenRead(file);
    var result = await ParquetSerializer.DeserializeAsync<Row>(stream);
    rows.AddRange(result.Data);
}

Console.WriteLine($"--- {rows.Count} rows ---");
foreach (var group in rows.GroupBy(r => r.Channel).OrderBy(g => g.Key))
{
    Console.WriteLine($"channel {group.Key,-8} {group.Count()}");
}
foreach (var group in rows.GroupBy(r => r.SourceSystem).OrderBy(g => g.Key))
{
    Console.WriteLine($"source  {group.Key,-16} {group.Count()}");
}
Console.WriteLine($"distinct event ids: {rows.Select(r => r.EventId).Distinct().Count()}");

class Row
{
    public string EventId { get; set; } = "";
    public string Channel { get; set; } = "";
    public string SourceSystem { get; set; } = "";
}
CS

sed -i "s|SAMPLE_ROOT|$repo/samples/landing|" Program.cs
dotnet run --project . 2>&1 | tail -30
