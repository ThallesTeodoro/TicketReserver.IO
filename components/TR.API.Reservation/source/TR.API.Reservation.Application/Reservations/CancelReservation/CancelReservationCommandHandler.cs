namespace TR.API.Reservation.Application.Reservations.CancelReservation;

using TR.API.Reservation.Application.Common.Messaging;
using TR.API.Reservation.Domain.Contracts.Caching;
using TR.API.Reservation.Domain.Contracts.Persistence;
using TR.API.Reservation.Domain.Entities;
using TR.API.Reservation.Domain.Enums;
using TR.API.Reservation.Domain.Exceptions;

public sealed class CancelReservationCommandHandler(
    IReservationRepository reservationRepository,
    IUnitOfWork unitOfWork,
    IHoldCacheService holdCacheService) : ICommandHandler<CancelReservationCommand, CancelReservationResponse>
{
    public async Task<CancelReservationResponse> HandleAsync(CancelReservationCommand command, CancellationToken cancellationToken = default)
    {
        var reservation = await reservationRepository.GetByIdAsync(command.ReservationId, cancellationToken)
            ?? throw new NotFoundException($"Reserva com Id '{command.ReservationId}' não foi encontrada.");

        if (reservation.CustomerId != command.CustomerId)
            throw new DomainException("Esta reserva pertence a outro cliente.");

        if (reservation.Status != ReservationStatus.AWAITING_PAYMENT)
            throw new ConflictException($"Não é possível cancelar diretamente uma reserva com status '{reservation.Status}'.");

        var now = DateTime.UtcNow;
        var reason = string.IsNullOrWhiteSpace(command.Reason) ? "USER_CANCELLED" : command.Reason.Trim();

        reservation.Cancel(reason, now);

        var items = reservation.Items.Select(i => new HoldItemDescriptor(i.TicketTierId, i.SeatId, i.Quantity));
        await holdCacheService.ReleaseReservationHoldsAsync(reservation.EventId, reservation.Id, items, cancellationToken);

        await unitOfWork.CommitAsync(cancellationToken);

        return new CancelReservationResponse(
            reservation.Id,
            reservation.Status.ToString(),
            reservation.CancellationReason,
            reservation.CancelledAt!.Value);
    }
}
