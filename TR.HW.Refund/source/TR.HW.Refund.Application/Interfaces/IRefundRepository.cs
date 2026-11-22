namespace TR.HW.Refund.Application.Interfaces;

using TR.HW.Refund.Domain.Entities;

public interface IRefundRepository
{
    Task<bool> IsMessageProcessedAsync(string messageId, string consumerGroup, CancellationToken ct = default);
    Task<PaymentSnapshot?> GetPaymentByReservationIdAsync(Guid reservationId, CancellationToken ct = default);
    Task SaveRefundTransactionallyAsync(Refund refund, string messageId, string consumerGroup, CancellationToken ct = default);
    Task RecordProcessedMessageAsync(string messageId, string consumerGroup, string eventType, CancellationToken ct = default);
}
