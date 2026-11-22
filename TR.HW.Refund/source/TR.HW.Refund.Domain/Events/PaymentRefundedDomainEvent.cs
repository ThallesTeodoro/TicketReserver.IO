namespace TR.HW.Refund.Domain.Events;

public interface IDomainEvent
{
    DateTime OccurredOn { get; }
}

public sealed record PaymentRefundedDomainEvent(
    Guid RefundId,
    Guid PaymentId,
    Guid ReservationId,
    Guid CustomerId,
    decimal Amount,
    string Currency,
    string GatewayRefundId,
    string Reason,
    DateTime OccurredOn) : IDomainEvent;
