namespace TR.HW.Payment.Application.Interfaces;

using TR.HW.Payment.Domain.Events;

public interface IKafkaPaymentProducer
{
    Task PublishPaymentApprovedAsync(PaymentApprovedDomainEvent domainEvent, string? traceparent = null, CancellationToken ct = default);
    Task PublishPaymentDeclinedAsync(PaymentDeclinedDomainEvent domainEvent, string? traceparent = null, CancellationToken ct = default);
}
