namespace TR.API.Reservation.Application.Reservations.HoldReservation;

using TR.API.Reservation.Application.Common.Messaging;

public record HoldReservationCommand(
    Guid CustomerId,
    Guid EventId,
    IReadOnlyCollection<HoldReservationItemDto> Items,
    string Currency = "BRL") : ICommand<HoldReservationResponse>;
