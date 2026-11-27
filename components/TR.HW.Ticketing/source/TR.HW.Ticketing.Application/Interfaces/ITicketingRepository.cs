namespace TR.HW.Ticketing.Application.Interfaces;

using TR.HW.Ticketing.Domain.Entities;
using TR.HW.Ticketing.Domain.Events;

public interface ITicketingRepository
{
    Task<bool> IsMessageProcessedAsync(string messageId, string consumerGroup, CancellationToken ct = default);
    Task<bool> HasExistingTicketsForReservationAsync(Guid reservationId, CancellationToken ct = default);
    Task SaveTicketsAndAllocationsTransactionallyAsync(
        IReadOnlyList<Ticket> tickets,
        IReadOnlyList<SeatAllocation> allocations,
        string messageId,
        string consumerGroup,
        CancellationToken ct = default);
    Task RecordTicketingFailedTransactionallyAsync(
        TicketingFailedDomainEvent failedEvent,
        string messageId,
        string consumerGroup,
        CancellationToken ct = default);
    Task RecordProcessedMessageAsync(string messageId, string consumerGroup, string eventType, CancellationToken ct = default);
}
