namespace TR.HW.Payment.Domain.Events;

public interface IDomainEvent
{
    DateTime OccurredOn { get; }
}

public sealed record PaymentApprovedDomainEvent(
    Guid PaymentId,
    Guid ReservationId,
    Guid CustomerId,
    decimal Amount,
    string Currency,
    string GatewayTransactionId,
    string AuthorizationCode,
    DateTime OccurredOn) : IDomainEvent;

public sealed record PaymentDeclinedDomainEvent(
    Guid PaymentId,
    Guid ReservationId,
    Guid CustomerId,
    decimal Amount,
    string FailureReason,
    DateTime OccurredOn) : IDomainEvent;
