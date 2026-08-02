using BunkerFlow.Contracts;

namespace BunkerFlow.Integration.Tests.TestSupport;

/// <summary>
/// Builds source records for tests. Starts from a record that normalizes and
/// validates cleanly, so each test only states the one thing it is about.
/// </summary>
public sealed class RecordBuilder
{
    /// <summary>An IMO number whose check digit is correct: 9074729.</summary>
    public const string ValidImo = "9074729";

    /// <summary>Same number with the check digit wrong, for the negative case.</summary>
    public const string InvalidImo = "9074720";

    private readonly Dictionary<string, string?> _fields = new(StringComparer.Ordinal)
    {
        ["tradeReference"] = "TR-100045",
        ["vesselImo"] = ValidImo,
        ["port"] = "DKFRC",
        ["product"] = "VLSFO",
        ["quantityMt"] = "850.5",
        ["priceUsdPerMt"] = "612.25",
        ["counterparty"] = "Bunker Holding A/S",
        ["tradedAtUtc"] = "2026-08-01T08:30:00Z",
    };

    private string _sourceSystem = "trading-desk";
    private string _sourceRecordId = "TD-1";
    private IngestionChannel _channel = IngestionChannel.Batch;

    public static RecordBuilder Valid() => new();

    public RecordBuilder With(string field, string? value)
    {
        _fields[field] = value;
        return this;
    }

    public RecordBuilder Without(string field)
    {
        _fields.Remove(field);
        return this;
    }

    /// <summary>Replaces every field, for testing a source system with its own names.</summary>
    public RecordBuilder WithFieldsOnly(Dictionary<string, string?> fields)
    {
        _fields.Clear();
        foreach (var (key, value) in fields)
        {
            _fields[key] = value;
        }

        return this;
    }

    public RecordBuilder From(string sourceSystem, string sourceRecordId)
    {
        _sourceSystem = sourceSystem;
        _sourceRecordId = sourceRecordId;
        return this;
    }

    public RecordBuilder Via(IngestionChannel channel)
    {
        _channel = channel;
        return this;
    }

    public SourceRecord Build() => new()
    {
        SourceSystem = _sourceSystem,
        SourceRecordId = _sourceRecordId,
        Channel = _channel,
        Fields = new Dictionary<string, string?>(_fields, StringComparer.Ordinal),
    };
}
