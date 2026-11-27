namespace TR.HW.ReservationOutboxRelay.Persistence;

using System.Data;
using Dapper;

public sealed class OutboxRepository(IDbConnectionFactory connectionFactory) : IOutboxRepository
{
    public async Task<IReadOnlyList<OutboxMessageRecord>> GetPendingBatchAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                id AS Id,
                correlation_id AS CorrelationId,
                event_type AS EventType,
                aggregate_type AS AggregateType,
                aggregate_id AS AggregateId,
                payload AS Payload,
                delay_seconds AS DelaySeconds,
                deliver_after AS DeliverAfter,
                status AS Status,
                retry_count AS RetryCount,
                error_message AS ErrorMessage,
                processed_at AS ProcessedAt,
                created_at AS CreatedAt
            FROM outbox_messages
            WHERE status = 0
              AND deliver_after <= @UtcNow
            ORDER BY created_at ASC
            LIMIT @BatchSize
            FOR UPDATE SKIP LOCKED;
            """;

        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<OutboxMessageRecord>(
            new CommandDefinition(sql, new { UtcNow = DateTime.UtcNow, BatchSize = batchSize }, cancellationToken: cancellationToken));

        return rows.ToList().AsReadOnly();
    }

    public async Task MarkAsPublishedAsync(Guid id, DateTime processedAt, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE outbox_messages
            SET status = 1,
                processed_at = @ProcessedAt,
                error_message = NULL
            WHERE id = @Id;
            """;

        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);

        await connection.ExecuteAsync(
            new CommandDefinition(sql, new { Id = id, ProcessedAt = processedAt }, cancellationToken: cancellationToken));
    }

    public async Task MarkAsFailedAsync(Guid id, string errorMessage, DateTime processedAt, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE outbox_messages
            SET status = 2,
                error_message = @ErrorMessage,
                processed_at = @ProcessedAt
            WHERE id = @Id;
            """;

        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);

        await connection.ExecuteAsync(
            new CommandDefinition(sql, new { Id = id, ErrorMessage = errorMessage, ProcessedAt = processedAt }, cancellationToken: cancellationToken));
    }

    public async Task IncrementRetryAsync(Guid id, string errorMessage, int nextDelaySeconds, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE outbox_messages
            SET retry_count = retry_count + 1,
                error_message = @ErrorMessage,
                status = 0,
                deliver_after = @DeliverAfter
            WHERE id = @Id;
            """;

        var deliverAfter = DateTime.UtcNow.AddSeconds(nextDelaySeconds);

        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);

        await connection.ExecuteAsync(
            new CommandDefinition(sql, new { Id = id, ErrorMessage = errorMessage, DeliverAfter = deliverAfter }, cancellationToken: cancellationToken));
    }
}
