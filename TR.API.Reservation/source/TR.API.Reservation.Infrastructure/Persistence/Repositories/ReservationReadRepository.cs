namespace TR.API.Reservation.Infrastructure.Persistence.Repositories;

using Dapper;
using TR.API.Reservation.Domain.Contracts.Persistence;
using TR.API.Reservation.Infrastructure.Persistence;

public sealed class ReservationReadRepository(IDbConnectionFactory connectionFactory) : IReservationReadRepository
{
    public async Task<ReservationReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string resSql = """
            SELECT
                r.id AS Id,
                r.customer_id AS CustomerId,
                r.event_id AS EventId,
                r.status::text AS Status,
                r.total_amount AS TotalAmount,
                r.currency AS Currency,
                r.hold_expires_at AS HoldExpiresAt,
                r.payment_attempted_at AS PaymentAttemptedAt,
                r.confirmed_at AS ConfirmedAt,
                r.cancelled_at AS CancelledAt,
                r.cancellation_reason AS CancellationReason,
                r.version AS Version,
                r.created_at AS CreatedAt,
                r.updated_at AS UpdatedAt
            FROM reservations r
            WHERE r.id = @Id;
            """;

        const string itemsSql = """
            SELECT
                i.id AS Id,
                i.ticket_tier_id AS TicketTierId,
                i.seat_id AS SeatId,
                i.seat_label AS SeatLabel,
                i.quantity AS Quantity,
                i.unit_price AS UnitPrice,
                i.subtotal AS Subtotal,
                i.created_at AS CreatedAt
            FROM reservation_items i
            WHERE i.reservation_id = @ReservationId
            ORDER BY i.created_at ASC;
            """;

        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);

        var resRow = await connection.QuerySingleOrDefaultAsync<ReservationRow>(
            new CommandDefinition(resSql, new { Id = id }, cancellationToken: cancellationToken));

        if (resRow is null)
        {
            return null;
        }

        var itemRows = await connection.QueryAsync<ReservationItemRow>(
            new CommandDefinition(itemsSql, new { ReservationId = id }, cancellationToken: cancellationToken));

        var items = itemRows.Select(i => new ReservationItemReadModel(
            i.Id,
            i.TicketTierId,
            i.SeatId,
            i.SeatLabel,
            i.Quantity,
            i.UnitPrice,
            i.Subtotal,
            i.CreatedAt)).ToList().AsReadOnly();

        var now = DateTime.UtcNow;
        var remainingSeconds = Math.Max(0, (int)(resRow.HoldExpiresAt - now).TotalSeconds);
        var isExpired = resRow.Status == "EXPIRED" || (resRow.Status == "AWAITING_PAYMENT" && now >= resRow.HoldExpiresAt);

        return new ReservationReadModel(
            resRow.Id,
            resRow.CustomerId,
            resRow.EventId,
            resRow.Status,
            resRow.TotalAmount,
            resRow.Currency,
            resRow.HoldExpiresAt,
            resRow.PaymentAttemptedAt,
            resRow.ConfirmedAt,
            resRow.CancelledAt,
            resRow.CancellationReason,
            resRow.Version,
            resRow.CreatedAt,
            resRow.UpdatedAt,
            remainingSeconds,
            isExpired,
            items);
    }

    private sealed class ReservationRow
    {
        public Guid Id { get; init; }
        public Guid CustomerId { get; init; }
        public Guid EventId { get; init; }
        public string Status { get; init; } = string.Empty;
        public decimal TotalAmount { get; init; }
        public string Currency { get; init; } = string.Empty;
        public DateTime HoldExpiresAt { get; init; }
        public DateTime? PaymentAttemptedAt { get; init; }
        public DateTime? ConfirmedAt { get; init; }
        public DateTime? CancelledAt { get; init; }
        public string? CancellationReason { get; init; }
        public int Version { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; init; }
    }

    private sealed class ReservationItemRow
    {
        public Guid Id { get; init; }
        public Guid TicketTierId { get; init; }
        public Guid? SeatId { get; init; }
        public string? SeatLabel { get; init; }
        public int Quantity { get; init; }
        public decimal UnitPrice { get; init; }
        public decimal Subtotal { get; init; }
        public DateTime CreatedAt { get; init; }
    }
}
