namespace TR.API.Reservation.Application.UseCases.CreateReservation;

public record CreateReservationCommand(
    Guid UserId,
    Guid EventId,
    string SeatNumber,
    decimal Price);
