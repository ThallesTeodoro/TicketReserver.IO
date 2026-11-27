namespace TR.API.Reservation.Application.Reservations.GetReservationById;

using TR.API.Reservation.Application.Common.Messaging;

public record GetReservationByIdQuery(Guid ReservationId) : IQuery<ReservationDetailsResponse>;
