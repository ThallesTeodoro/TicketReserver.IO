namespace TR.API.Reservation.Application.Reservations.HoldReservation;

public record HoldReservationItemDto(
    Guid TicketTierId,
    Guid? SeatId,
    string? SeatLabel,
    int Quantity,
    decimal UnitPrice);

public record HoldReservationItemResponse(
    Guid Id,
    Guid TicketTierId,
    Guid? SeatId,
    string? SeatLabel,
    int Quantity,
    decimal UnitPrice,
    decimal Subtotal);

public record HoldReservationResponse(
    Guid Id,
    Guid CustomerId,
    Guid EventId,
    string Status,
    decimal TotalAmount,
    string Currency,
    DateTime HoldExpiresAt,
    IReadOnlyCollection<HoldReservationItemResponse> Items);
