namespace TR.API.Reservation.Application.Reservations.CancelReservation;

using TR.API.Reservation.Application.Common.Messaging;

public record CancelReservationCommand(
    Guid ReservationId,
    Guid CustomerId,
    string? Reason = null) : ICommand<CancelReservationResponse>;
