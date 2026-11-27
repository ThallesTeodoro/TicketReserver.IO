namespace TR.HW.ResCompensator.Infrastructure.Persistence;

using Dapper;
using TR.HW.ResCompensator.Application.Features.ReconcileReservation;
using TR.HW.ResCompensator.Application.Interfaces;
using TR.HW.ResCompensator.Domain.Entities;
using TR.HW.ResCompensator.Domain.Enums;

public sealed class ReservationCompensationRepository(IDbConnectionFactory connectionFactory)
    : IReservationCompensationRepository
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

    public async Task<ReservationState?> GetReservationByIdAsync(Guid reservationId, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateConnectionAsync(ct);
        const string reservationSql = """
            SELECT
                id AS Id,
                customer_id AS CustomerId,
                event_id AS EventId,
                status::text AS StatusStr,
                total_amount AS TotalAmount,
                currency AS Currency,
                confirmed_at AS ConfirmedAt,
                cancelled_at AS CancelledAt,
                cancellation_reason AS CancellationReason,
                version AS Version,
                updated_at AS UpdatedAt
            FROM reservations
            WHERE id = @ReservationId
            LIMIT 1;
            """;

        var row = await connection.QueryFirstOrDefaultAsync<ReservationRow>(
            new CommandDefinition(reservationSql, new { ReservationId = reservationId }, cancellationToken: ct));

        if (row is null)
        {
            return null;
        }

        const string itemsSql = """
            SELECT
                id AS Id,
                reservation_id AS ReservationId,
                ticket_tier_id AS TicketTierId,
                seat_id AS SeatId,
                seat_label AS SeatLabel,
                quantity AS Quantity
            FROM reservation_items
            WHERE reservation_id = @ReservationId;
            """;

        var itemRows = await connection.QueryAsync<ReservationItemRow>(
            new CommandDefinition(itemsSql, new { ReservationId = reservationId }, cancellationToken: ct));

        var items = itemRows.Select(i => ReservedSeatItem.Create(
            i.Id,
            i.ReservationId,
            i.TicketTierId,
            i.SeatId,
            i.SeatLabel,
            i.Quantity)).ToList();

        var status = row.StatusStr switch
        {
            "CONFIRMED" => ReservationStatus.Confirmed,
            "CANCELLED" => ReservationStatus.Cancelled,
            "EXPIRED" => ReservationStatus.Expired,
            "PROCESSING_PAYMENT" => ReservationStatus.ProcessingPayment,
            _ => ReservationStatus.AwaitingPayment
        };

        return ReservationState.Reconstitute(
            id: row.Id,
            customerId: row.CustomerId,
            eventId: row.EventId,
            status: status,
            totalAmount: row.TotalAmount,
            currency: row.Currency,
            version: row.Version,
            items: items,
            confirmedAt: row.ConfirmedAt,
            cancelledAt: row.CancelledAt,
            cancellationReason: row.CancellationReason,
            updatedAt: row.UpdatedAt);
    }

    public async Task SaveReservationStateTransactionallyAsync(
        ReservationState reservation,
        string messageId,
        string consumerGroup,
        string eventType,
        CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        try
        {
            const string updateReservationSql = """
                UPDATE reservations
                SET status = @Status::reservation_status_enum,
                    confirmed_at = @ConfirmedAt,
                    cancelled_at = @CancelledAt,
                    cancellation_reason = @CancellationReason,
                    version = @Version,
                    updated_at = @UpdatedAt
                WHERE id = @Id;
                """;

            await connection.ExecuteAsync(new CommandDefinition(
                updateReservationSql,
                new
                {
                    reservation.Id,
                    Status = reservation.Status.ToDatabaseStatusString(),
                    reservation.ConfirmedAt,
                    reservation.CancelledAt,
                    reservation.CancellationReason,
                    reservation.Version,
                    reservation.UpdatedAt
                },
                transaction: transaction,
                cancellationToken: ct));

            const string insertInboxSql = """
                INSERT INTO inbox_messages (message_id, consumer_group, event_type, processed_at)
                VALUES (@MessageId, @ConsumerGroup, @EventType, NOW())
                ON CONFLICT (message_id, consumer_group) DO NOTHING;
                """;

            await connection.ExecuteAsync(new CommandDefinition(
                insertInboxSql,
                new { MessageId = messageId, ConsumerGroup = consumerGroup, EventType = eventType },
                transaction: transaction,
                cancellationToken: ct));

            await transaction.CommitAsync(ct);
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
        const string sql = """
            INSERT INTO inbox_messages (message_id, consumer_group, event_type, processed_at)
            VALUES (@MessageId, @ConsumerGroup, @EventType, NOW())
            ON CONFLICT (message_id, consumer_group) DO NOTHING;
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { MessageId = messageId, ConsumerGroup = consumerGroup, EventType = eventType },
            cancellationToken: ct));
    }

    private sealed class ReservationRow
    {
        public Guid Id { get; set; }
        public Guid CustomerId { get; set; }
        public Guid EventId { get; set; }
        public string StatusStr { get; set; } = "AWAITING_PAYMENT";
        public decimal TotalAmount { get; set; }
        public string Currency { get; set; } = "BRL";
        public DateTime? ConfirmedAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        public string? CancellationReason { get; set; }
        public int Version { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    private sealed class ReservationItemRow
    {
        public Guid Id { get; set; }
        public Guid ReservationId { get; set; }
        public Guid TicketTierId { get; set; }
        public Guid? SeatId { get; set; }
        public string? SeatLabel { get; set; }
        public int Quantity { get; set; }
    }
}
