namespace TR.HW.ResCompensator.Domain.Entities;

using TR.HW.ResCompensator.Domain.Exceptions;

public sealed class ReservedSeatItem
{
    public Guid Id { get; private set; }
    public Guid ReservationId { get; private set; }
    public Guid TicketTierId { get; private set; }
    public Guid? SeatId { get; private set; }
    public string? SeatLabel { get; private set; }
    public int Quantity { get; private set; }

    private ReservedSeatItem() { }

    public static ReservedSeatItem Create(
        Guid id,
        Guid reservationId,
        Guid ticketTierId,
        Guid? seatId,
        string? seatLabel,
        int quantity = 1)
    {
        if (reservationId == Guid.Empty)
        {
            throw new DomainException("ReservationId cannot be empty.");
        }

        if (ticketTierId == Guid.Empty)
        {
            throw new DomainException("TicketTierId cannot be empty.");
        }

        if (quantity <= 0)
        {
            throw new DomainException("Quantity must be greater than zero.");
        }

        return new ReservedSeatItem
        {
            Id = id == Guid.Empty ? Guid.NewGuid() : id,
            ReservationId = reservationId,
            TicketTierId = ticketTierId,
            SeatId = seatId,
            SeatLabel = seatLabel?.Trim(),
            Quantity = quantity
        };
    }
}
