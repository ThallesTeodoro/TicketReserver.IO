namespace TR.API.Reservation.Application.Reservations.CancelReservation;

public record CancelReservationResponse(
    Guid ReservationId,
    string Status,
    string? CancellationReason,
    DateTime CancelledAt);
