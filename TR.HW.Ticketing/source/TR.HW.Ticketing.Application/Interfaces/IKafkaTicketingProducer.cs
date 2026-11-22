namespace TR.HW.Ticketing.Application.Interfaces;

using TR.HW.Ticketing.Domain.Events;

public interface IKafkaTicketingProducer
{
    Task PublishTicketsIssuedAsync(
        Guid reservationId,
        Guid paymentId,
        Guid customerId,
        Guid eventId,
        IReadOnlyList<TicketIssuedDomainEvent> ticketEvents,
        string? traceparent = null,
        CancellationToken ct = default);

    Task PublishTicketingFailedAsync(
        TicketingFailedDomainEvent domainEvent,
        string? traceparent = null,
        CancellationToken ct = default);
}
