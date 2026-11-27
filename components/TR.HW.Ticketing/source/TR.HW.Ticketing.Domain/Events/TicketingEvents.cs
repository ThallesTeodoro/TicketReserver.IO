namespace TR.HW.Ticketing.Domain.Events;

public interface IDomainEvent
{
    DateTime OccurredOn { get; }
}

public sealed record TicketIssuedDomainEvent(
    Guid TicketId,
    string TicketCode,
    string QrCodeHash,
    Guid ReservationId,
    Guid PaymentId,
    Guid CustomerId,
    Guid EventId,
    Guid TicketTierId,
    Guid? SeatId,
    string? SeatLabel,
    decimal PricePaid,
    DateTime OccurredOn) : IDomainEvent;

public sealed record TicketingFailedDomainEvent(
    Guid ReservationId,
    Guid PaymentId,
    Guid CustomerId,
    Guid EventId,
    string FailureReason,
    DateTime OccurredOn) : IDomainEvent;
