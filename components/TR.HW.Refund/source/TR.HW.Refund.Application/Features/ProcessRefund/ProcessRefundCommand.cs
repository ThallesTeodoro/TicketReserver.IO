namespace TR.HW.Refund.Application.Features.ProcessRefund;

using TR.HW.Refund.Application.Common.Messaging;

public sealed record ProcessRefundCommand(
    Guid ReservationId,
    Guid? PaymentId,
    Guid? CustomerId,
    string Reason,
    string? MessageId = null,
    string? Traceparent = null) : ICommand<ProcessRefundResult>;

public sealed record ProcessRefundResult(
    bool IsRefunded,
    Guid? RefundId,
    string Status,
    string? GatewayRefundId,
    string? FailureReason = null);
