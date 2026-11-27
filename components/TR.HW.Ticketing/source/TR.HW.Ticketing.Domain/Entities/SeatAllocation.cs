namespace TR.HW.Ticketing.Domain.Entities;

using TR.HW.Ticketing.Domain.Enums;
using TR.HW.Ticketing.Domain.Exceptions;

public sealed class SeatAllocation
{
    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public Guid SeatId { get; private set; }
    public Guid ReservationId { get; private set; }
    public Guid TicketId { get; private set; }
    public AllocationType AllocationType { get; private set; }
    public DateTime AllocatedAt { get; private set; }

    private SeatAllocation() { }

    public static SeatAllocation Create(
        Guid eventId,
        Guid seatId,
        Guid reservationId,
        Guid ticketId,
        AllocationType allocationType = AllocationType.NumberedSeat)
    {
        if (eventId == Guid.Empty)
        {
            throw new DomainException("EventId cannot be empty.");
        }

        if (seatId == Guid.Empty)
        {
            throw new DomainException("SeatId cannot be empty.");
        }

        if (reservationId == Guid.Empty)
        {
            throw new DomainException("ReservationId cannot be empty.");
        }

        if (ticketId == Guid.Empty)
        {
            throw new DomainException("TicketId cannot be empty.");
        }

        return new SeatAllocation
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            SeatId = seatId,
            ReservationId = reservationId,
            TicketId = ticketId,
            AllocationType = allocationType,
            AllocatedAt = DateTime.UtcNow
        };
    }
}
