using Npgsql;

namespace BunkerFlow.Integration.Idempotency;

/// <summary>
/// Durable dedupe store. The primary key does the work: two workers racing on
/// the same business key both run the same INSERT, and Postgres lets exactly
/// one of them insert a row.
/// </summary>
public sealed class PostgresDeduplicationStore : IDeduplicationStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresDeduplicationStore(NpgsqlDataSource dataSource) => _dataSource = dataSource;

    public async Task<bool> TryReserveAsync(string deduplicationKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deduplicationKey);

        const string sql = """
            INSERT INTO ingested_keys (deduplication_key)
            VALUES (@key)
            ON CONFLICT (deduplication_key) DO NOTHING;
            """;

        await using var command = _dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("key", deduplicationKey);

        var inserted = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return inserted == 1;
    }

    public async Task ReleaseAsync(string deduplicationKey, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deduplicationKey);

        await using var command = _dataSource.CreateCommand(
            "DELETE FROM ingested_keys WHERE deduplication_key = @key");
        command.Parameters.AddWithValue("key", deduplicationKey);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
