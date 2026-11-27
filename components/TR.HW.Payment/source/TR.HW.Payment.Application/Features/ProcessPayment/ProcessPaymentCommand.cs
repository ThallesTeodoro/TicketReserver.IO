namespace TR.HW.Payment.Application.Features.ProcessPayment;

using TR.HW.Payment.Application.Common.Messaging;

public sealed record ProcessPaymentCommand(
    Guid ReservationId,
    Guid CustomerId,
    decimal TotalAmount,
    string Currency,
    string PaymentMethod,
    string? CardLastFour = null,
    string? CardBrand = null,
    string? MessageId = null,
    string? Traceparent = null) : ICommand<ProcessPaymentResult>;

public sealed record ProcessPaymentResult(
    bool IsApproved,
    Guid PaymentId,
    string Status,
    string? GatewayTransactionId,
    string? FailureReason);
