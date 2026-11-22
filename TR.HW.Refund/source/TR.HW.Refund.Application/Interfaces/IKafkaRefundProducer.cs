namespace TR.HW.Refund.Application.Interfaces;

using TR.HW.Refund.Domain.Events;

public interface IKafkaRefundProducer
{
    Task PublishPaymentRefundedAsync(PaymentRefundedDomainEvent domainEvent, string? traceparent = null, CancellationToken ct = default);
}
