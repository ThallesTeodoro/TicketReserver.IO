namespace TR.API.Reservation.Application.Reservations.GetReservationById;

using TR.API.Reservation.Application.Common.Messaging;
using TR.API.Reservation.Domain.Contracts.Persistence;
using TR.API.Reservation.Domain.Exceptions;

public sealed class GetReservationByIdQueryHandler(
    IReservationReadRepository readRepository) : IQueryHandler<GetReservationByIdQuery, ReservationDetailsResponse>
{
    public async Task<ReservationDetailsResponse> HandleAsync(GetReservationByIdQuery query, CancellationToken cancellationToken = default)
    {
        var readModel = await readRepository.GetByIdAsync(query.ReservationId, cancellationToken)
            ?? throw new NotFoundException($"Reserva com Id '{query.ReservationId}' não foi encontrada.");

        var items = readModel.Items.Select(i => new ReservationItemDetailsResponse(
            i.Id,
            i.TicketTierId,
            i.SeatId,
            i.SeatLabel,
            i.Quantity,
            i.UnitPrice,
            i.Subtotal,
            i.CreatedAt)).ToList().AsReadOnly();

        return new ReservationDetailsResponse(
            readModel.Id,
            readModel.CustomerId,
            readModel.EventId,
            readModel.Status,
            readModel.TotalAmount,
            readModel.Currency,
            readModel.HoldExpiresAt,
            readModel.PaymentAttemptedAt,
            readModel.ConfirmedAt,
            readModel.CancelledAt,
            readModel.CancellationReason,
            readModel.Version,
            readModel.CreatedAt,
            readModel.UpdatedAt,
            readModel.RemainingHoldSeconds,
            readModel.IsExpired,
            items);
    }
}
