namespace TR.HW.Payment.Application.Interfaces;

using TR.HW.Payment.Domain.Entities;

public interface IPaymentRepository
{
    Task<bool> IsMessageProcessedAsync(string messageId, string consumerGroup, CancellationToken ct = default);
    Task<bool> HasExistingPaymentForReservationAsync(Guid reservationId, CancellationToken ct = default);
    Task SavePaymentTransactionallyAsync(Payment payment, string messageId, string consumerGroup, CancellationToken ct = default);
    Task RecordProcessedMessageAsync(string messageId, string consumerGroup, string eventType, CancellationToken ct = default);
}
