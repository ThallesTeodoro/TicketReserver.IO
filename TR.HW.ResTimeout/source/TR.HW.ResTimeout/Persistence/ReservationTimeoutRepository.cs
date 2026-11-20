namespace TR.HW.ResTimeout.Persistence;

using Dapper;

public sealed class ReservationTimeoutRepository(IDbConnectionFactory connectionFactory) : IReservationTimeoutRepository
{
    public async Task<bool> IsMessageProcessedAsync(string messageId, string consumerGroup, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateConnectionAsync(ct);
        const string sql = """
            SELECT 1 FROM inbox_messages
            WHERE message_id = @MessageId AND consumer_group = @ConsumerGroup
            LIMIT 1;
            """;

        var result = await connection.ExecuteScalarAsync<int?>(
            new CommandDefinition(sql, new { MessageId = messageId, ConsumerGroup = consumerGroup }, cancellationToken: ct));

        return result.HasValue;
    }

    public async Task<ReservationTimeoutData?> GetReservationAsync(Guid reservationId, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateConnectionAsync(ct);
        const string reservationSql = """
            SELECT id, status::text, event_id, customer_id
            FROM reservations
            WHERE id = @ReservationId;
            """;

        var res = await connection.QuerySingleOrDefaultAsync<dynamic>(
            new CommandDefinition(reservationSql, new { ReservationId = reservationId }, cancellationToken: ct));

        if (res is null)
        {
            return null;
        }

        const string itemsSql = """
            SELECT seat_id, seat_label
            FROM reservation_items
            WHERE reservation_id = @ReservationId AND seat_id IS NOT NULL;
            """;

        var items = await connection.QueryAsync<dynamic>(
            new CommandDefinition(itemsSql, new { ReservationId = reservationId }, cancellationToken: ct));

        var seatItems = new List<ReservationSeatItemData>();
        foreach (var item in items)
        {
            seatItems.Add(new ReservationSeatItemData((Guid)item.seat_id, (string?)item.seat_label));
        }

        return new ReservationTimeoutData(
            (Guid)res.id,
            (string)res.status,
            (Guid)res.event_id,
            (Guid)res.customer_id,
            seatItems);
    }

    public async Task<bool> ExpireReservationAsync(Guid reservationId, string messageId, string consumerGroup, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        try
        {
            const string checkStatusSql = """
                SELECT status::text FROM reservations
                WHERE id = @ReservationId
                FOR UPDATE;
                """;

            var currentStatus = await connection.ExecuteScalarAsync<string>(
                new CommandDefinition(checkStatusSql, new { ReservationId = reservationId }, transaction: transaction, cancellationToken: ct));

            if (currentStatus is null || !string.Equals(currentStatus, "AWAITING_PAYMENT", StringComparison.OrdinalIgnoreCase))
            {
                const string insertInboxSql = """
                    INSERT INTO inbox_messages (message_id, consumer_group, event_type, processed_at)
                    VALUES (@MessageId, @ConsumerGroup, 'reservation.v1.hold_created.delayed', NOW())
                    ON CONFLICT (message_id, consumer_group) DO NOTHING;
                    """;

                await connection.ExecuteAsync(
                    new CommandDefinition(insertInboxSql, new { MessageId = messageId, ConsumerGroup = consumerGroup }, transaction: transaction, cancellationToken: ct));

                await transaction.CommitAsync(ct);
                return false;
            }

            const string updateSql = """
                UPDATE reservations
                SET status = 'EXPIRED'::reservation_status_enum,
                    cancellation_reason = 'PAYMENT_TIMEOUT',
                    updated_at = NOW()
                WHERE id = @ReservationId;

                INSERT INTO inbox_messages (message_id, consumer_group, event_type, processed_at)
                VALUES (@MessageId, @ConsumerGroup, 'reservation.v1.hold_created.delayed', NOW())
                ON CONFLICT (message_id, consumer_group) DO NOTHING;
                """;

            await connection.ExecuteAsync(
                new CommandDefinition(updateSql, new { ReservationId = reservationId, MessageId = messageId, ConsumerGroup = consumerGroup }, transaction: transaction, cancellationToken: ct));

            await transaction.CommitAsync(ct);
            return true;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task RecordProcessedMessageAsync(string messageId, string consumerGroup, string eventType, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateConnectionAsync(ct);
        const string insertInboxSql = """
            INSERT INTO inbox_messages (message_id, consumer_group, event_type, processed_at)
            VALUES (@MessageId, @ConsumerGroup, @EventType, NOW())
            ON CONFLICT (message_id, consumer_group) DO NOTHING;
            """;

        await connection.ExecuteAsync(
            new CommandDefinition(insertInboxSql, new { MessageId = messageId, ConsumerGroup = consumerGroup, EventType = eventType }, cancellationToken: ct));
    }
}
