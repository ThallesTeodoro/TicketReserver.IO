namespace TR.HW.Ticketing.Domain.Exceptions;

public class DomainException(string message) : Exception(message);

public sealed class SeatAlreadyAllocatedException(Guid eventId, Guid seatId)
    : DomainException($"Seat '{seatId}' is already allocated for event '{eventId}'.")
{
    public Guid EventId { get; } = eventId;
    public Guid SeatId { get; } = seatId;
}
