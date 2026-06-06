using System.Diagnostics;
using TR.API.Reservation.Application.UseCases.CreateReservation;
using TR.API.Reservation.Domain.Repositories;
using ReservationEntity = TR.API.Reservation.Domain.Entities.Reservation;

namespace TR.API.Reservation.Application.UseCases.CreateReservation;

public class CreateReservationUseCase
{
    private static readonly ActivitySource ActivitySource = new("TR.API.Reservation");
    private readonly IReservationRepository _reservationRepository;

    public CreateReservationUseCase(IReservationRepository reservationRepository)
    {
        _reservationRepository = reservationRepository;
    }

    public async Task<ReservationOutput> ExecuteAsync(CreateReservationCommand command)
    {
        using var activity = ActivitySource.StartActivity("CreateReservationUseCase.Execute");
        activity?.SetTag("user.id", command.UserId);
        activity?.SetTag("event.id", command.EventId);

        var reservation = new ReservationEntity(
            command.UserId,
            command.EventId,
            command.SeatNumber,
            command.Price);

        await _reservationRepository.AddAsync(reservation);

        return reservation.ToOutput();
    }
}
