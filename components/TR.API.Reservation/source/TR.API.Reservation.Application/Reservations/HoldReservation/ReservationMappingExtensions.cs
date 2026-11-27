namespace TR.API.Reservation.Application.Reservations.HoldReservation;

using TR.API.Reservation.Domain.Entities;

public static class ReservationMappingExtensions
{
    public static HoldReservationResponse ToHoldResponse(this Reservation reservation)
    {
        var items = reservation.Items.Select(i => new HoldReservationItemResponse(
            i.Id,
            i.TicketTierId,
            i.SeatId,
            i.SeatLabel,
            i.Quantity,
            i.UnitPrice,
            i.Subtotal)).ToList().AsReadOnly();

        return new HoldReservationResponse(
            reservation.Id,
            reservation.CustomerId,
            reservation.EventId,
            reservation.Status.ToString(),
            reservation.TotalAmount,
            reservation.Currency,
            reservation.HoldExpiresAt,
            items);
    }
}
