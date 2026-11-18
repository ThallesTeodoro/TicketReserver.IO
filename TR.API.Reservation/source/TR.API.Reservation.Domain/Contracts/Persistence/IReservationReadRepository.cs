namespace TR.API.Reservation.Domain.Contracts.Persistence;

public record ReservationItemReadModel(
    Guid Id,
    Guid TicketTierId,
    Guid? SeatId,
    string? SeatLabel,
    int Quantity,
    decimal UnitPrice,
    decimal Subtotal,
    DateTime CreatedAt);

public record ReservationReadModel(
    Guid Id,
    Guid CustomerId,
    Guid EventId,
    string Status,
    decimal TotalAmount,
    string Currency,
    DateTime HoldExpiresAt,
    DateTime? PaymentAttemptedAt,
    DateTime? ConfirmedAt,
    DateTime? CancelledAt,
    string? CancellationReason,
    int Version,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int RemainingHoldSeconds,
    bool IsExpired,
    IReadOnlyCollection<ReservationItemReadModel> Items);

public interface IReservationReadRepository
{
    Task<ReservationReadModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
