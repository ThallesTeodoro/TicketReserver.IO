namespace TR.HW.ResCompensator.Application.Interfaces;

using TR.HW.ResCompensator.Domain.Entities;

public interface IReservationCompensationRepository
{
    Task<bool> IsMessageProcessedAsync(string messageId, string consumerGroup, CancellationToken ct = default);
    Task<ReservationState?> GetReservationByIdAsync(Guid reservationId, CancellationToken ct = default);
    Task SaveReservationStateTransactionallyAsync(
        ReservationState reservation,
        string messageId,
        string consumerGroup,
        string eventType,
        CancellationToken ct = default);
    Task RecordProcessedMessageAsync(string messageId, string consumerGroup, string eventType, CancellationToken ct = default);
}
