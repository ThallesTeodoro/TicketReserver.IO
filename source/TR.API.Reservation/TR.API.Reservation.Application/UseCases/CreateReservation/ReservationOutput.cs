namespace TR.API.Reservation.Application.UseCases.CreateReservation;

public record ReservationOutput(
    Guid Id,
    Guid UserId,
    Guid EventId,
    string SeatNumber,
    decimal Price,
    string Status,
    DateTime CreatedAt);
