using TR.API.Reservation.Domain.Repositories;
using ReservationEntity = TR.API.Reservation.Domain.Entities.Reservation;

namespace TR.API.Reservation.Application.UseCases.CreateReservation;

public class CreateReservationUseCase
{
    private readonly IReservationRepository _reservationRepository;

    public CreateReservationUseCase(IReservationRepository reservationRepository)
    {
        _reservationRepository = reservationRepository;
    }

    public async Task<ReservationOutput> ExecuteAsync(CreateReservationCommand command)
    {
        var reservation = new ReservationEntity(
            command.UserId,
            command.EventId,
            command.SeatNumber,
            command.Price);

        await _reservationRepository.AddAsync(reservation);

        return reservation.ToOutput();
    }
}
