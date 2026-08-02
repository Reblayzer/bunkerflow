using System.Text;
using BunkerFlow.Contracts;
using Npgsql;

namespace BunkerFlow.Integration.Landing;

/// <summary>
/// Postgres-backed landing store. Every statement is parameterized, appends are
/// idempotent on the event id, and reads are always paginated and indexed on the
/// columns the query endpoint filters by.
/// </summary>
public sealed class PostgresEventRepository : IEventRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresEventRepository(NpgsqlDataSource dataSource) => _dataSource = dataSource;

    /// <summary>
    /// Creates the landing table on startup. Real deployments would run this
    /// through a migration tool; the compose stack keeps it self-contained.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        const string ddl = """
            CREATE TABLE IF NOT EXISTS landed_events (
                event_id         TEXT        PRIMARY KEY,
                event_type       TEXT        NOT NULL,
                schema_version   INTEGER     NOT NULL,
                source_system    TEXT        NOT NULL,
                source_record_id TEXT        NOT NULL,
                channel          TEXT        NOT NULL,
                occurred_at_utc  TIMESTAMPTZ NOT NULL,
                ingested_at_utc  TIMESTAMPTZ NOT NULL,
                landed_at_utc    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                trade_reference  TEXT        NOT NULL,
                vessel_imo       TEXT        NOT NULL,
                port             TEXT        NOT NULL,
                product          TEXT        NOT NULL,
                quantity_mt      NUMERIC(12, 3) NOT NULL,
                price_usd_per_mt NUMERIC(12, 4) NOT NULL,
                counterparty     TEXT        NOT NULL,
                traded_at_utc    TIMESTAMPTZ NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_landed_events_occurred_at
                ON landed_events (occurred_at_utc DESC);
            CREATE INDEX IF NOT EXISTS ix_landed_events_source_system
                ON landed_events (source_system);
            CREATE INDEX IF NOT EXISTS ix_landed_events_port_product
                ON landed_events (port, product);

            CREATE TABLE IF NOT EXISTS ingested_keys (
                deduplication_key TEXT PRIMARY KEY,
                reserved_at_utc   TIMESTAMPTZ NOT NULL DEFAULT NOW()
            );
            """;

        await using var command = _dataSource.CreateCommand(ddl);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AppendAsync(IntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        const string sql = """
            INSERT INTO landed_events (
                event_id, event_type, schema_version, source_system, source_record_id,
                channel, occurred_at_utc, ingested_at_utc, trade_reference, vessel_imo,
                port, product, quantity_mt, price_usd_per_mt, counterparty, traded_at_utc)
            VALUES (
                @event_id, @event_type, @schema_version, @source_system, @source_record_id,
                @channel, @occurred_at_utc, @ingested_at_utc, @trade_reference, @vessel_imo,
                @port, @product, @quantity_mt, @price_usd_per_mt, @counterparty, @traded_at_utc)
            ON CONFLICT (event_id) DO NOTHING;
            """;

        var trade = integrationEvent.Payload;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("event_id", integrationEvent.EventId);
        command.Parameters.AddWithValue("event_type", integrationEvent.EventType);
        command.Parameters.AddWithValue("schema_version", integrationEvent.SchemaVersion);
        command.Parameters.AddWithValue("source_system", integrationEvent.SourceSystem);
        command.Parameters.AddWithValue("source_record_id", integrationEvent.SourceRecordId);
        command.Parameters.AddWithValue("channel", integrationEvent.Channel.ToString());
        command.Parameters.AddWithValue("occurred_at_utc", integrationEvent.OccurredAtUtc);
        command.Parameters.AddWithValue("ingested_at_utc", integrationEvent.IngestedAtUtc);
        command.Parameters.AddWithValue("trade_reference", trade.TradeReference);
        command.Parameters.AddWithValue("vessel_imo", trade.VesselImo);
        command.Parameters.AddWithValue("port", trade.Port);
        command.Parameters.AddWithValue("product", trade.Product);
        command.Parameters.AddWithValue("quantity_mt", trade.QuantityMt);
        command.Parameters.AddWithValue("price_usd_per_mt", trade.PriceUsdPerMt);
        command.Parameters.AddWithValue("counterparty", trade.Counterparty);
        command.Parameters.AddWithValue("traded_at_utc", trade.TradedAtUtc);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<IntegrationEvent>> QueryAsync(
        EventQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var normalized = query.Normalized();
        var sql = new StringBuilder("SELECT * FROM landed_events WHERE 1 = 1");

        if (normalized.SourceSystem is not null)
        {
            sql.Append(" AND source_system = @source_system");
        }

        if (normalized.Port is not null)
        {
            sql.Append(" AND port = @port");
        }

        if (normalized.Product is not null)
        {
            sql.Append(" AND product = @product");
        }

        if (normalized.FromUtc is not null)
        {
            sql.Append(" AND occurred_at_utc >= @from_utc");
        }

        if (normalized.ToUtc is not null)
        {
            sql.Append(" AND occurred_at_utc <= @to_utc");
        }

        sql.Append(" ORDER BY occurred_at_utc DESC, event_id LIMIT @limit OFFSET @offset");

        await using var command = _dataSource.CreateCommand(sql.ToString());

        if (normalized.SourceSystem is not null)
        {
            command.Parameters.AddWithValue("source_system", normalized.SourceSystem);
        }

        if (normalized.Port is not null)
        {
            command.Parameters.AddWithValue("port", normalized.Port.ToUpperInvariant());
        }

        if (normalized.Product is not null)
        {
            command.Parameters.AddWithValue("product", normalized.Product.ToUpperInvariant());
        }

        if (normalized.FromUtc is not null)
        {
            command.Parameters.AddWithValue("from_utc", normalized.FromUtc.Value);
        }

        if (normalized.ToUtc is not null)
        {
            command.Parameters.AddWithValue("to_utc", normalized.ToUtc.Value);
        }

        command.Parameters.AddWithValue("limit", normalized.Limit);
        command.Parameters.AddWithValue("offset", normalized.Offset);

        var events = new List<IntegrationEvent>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            events.Add(Map(reader));
        }

        return events;
    }

    public async Task<long> CountAsync(CancellationToken cancellationToken)
    {
        await using var command = _dataSource.CreateCommand("SELECT COUNT(*) FROM landed_events");
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is long count ? count : 0;
    }

    public async Task<bool> IsReachableAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var command = _dataSource.CreateCommand("SELECT 1");
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (NpgsqlException)
        {
            return false;
        }
    }

    private static IntegrationEvent Map(NpgsqlDataReader reader) => new()
    {
        EventId = reader.GetString(reader.GetOrdinal("event_id")),
        EventType = reader.GetString(reader.GetOrdinal("event_type")),
        SchemaVersion = reader.GetInt32(reader.GetOrdinal("schema_version")),
        SourceSystem = reader.GetString(reader.GetOrdinal("source_system")),
        SourceRecordId = reader.GetString(reader.GetOrdinal("source_record_id")),
        Channel = Enum.Parse<IngestionChannel>(reader.GetString(reader.GetOrdinal("channel"))),
        OccurredAtUtc = reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("occurred_at_utc")),
        IngestedAtUtc = reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("ingested_at_utc")),
        Payload = new BunkerTrade
        {
            TradeReference = reader.GetString(reader.GetOrdinal("trade_reference")),
            VesselImo = reader.GetString(reader.GetOrdinal("vessel_imo")),
            Port = reader.GetString(reader.GetOrdinal("port")),
            Product = reader.GetString(reader.GetOrdinal("product")),
            QuantityMt = reader.GetDecimal(reader.GetOrdinal("quantity_mt")),
            PriceUsdPerMt = reader.GetDecimal(reader.GetOrdinal("price_usd_per_mt")),
            Counterparty = reader.GetString(reader.GetOrdinal("counterparty")),
            TradedAtUtc = reader.GetFieldValue<DateTimeOffset>(reader.GetOrdinal("traded_at_utc")),
        },
    };
}
