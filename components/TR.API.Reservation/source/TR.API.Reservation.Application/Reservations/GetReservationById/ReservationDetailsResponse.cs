namespace TR.API.Reservation.Application.Reservations.GetReservationById;

public record ReservationItemDetailsResponse(
    Guid Id,
    Guid TicketTierId,
    Guid? SeatId,
    string? SeatLabel,
    int Quantity,
    decimal UnitPrice,
    decimal Subtotal,
    DateTime CreatedAt);

public record ReservationDetailsResponse(
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
    IReadOnlyCollection<ReservationItemDetailsResponse> Items);
