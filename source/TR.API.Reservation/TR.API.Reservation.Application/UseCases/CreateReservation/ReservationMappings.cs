using ReservationEntity = TR.API.Reservation.Domain.Entities.Reservation;

namespace TR.API.Reservation.Application.UseCases.CreateReservation;

public static class ReservationMappings
{
    public static ReservationOutput ToOutput(this ReservationEntity entity)
    {
        return new ReservationOutput(
            entity.Id,
            entity.UserId,
            entity.EventId,
            entity.SeatNumber,
            entity.Price,
            entity.Status.ToString(),
            entity.CreatedAt);
    }
}
