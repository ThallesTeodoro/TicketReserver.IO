namespace TR.HW.Refund.Domain.Entities;

using TR.HW.Refund.Domain.Enums;

public sealed record PaymentSnapshot(
    Guid Id,
    Guid ReservationId,
    Guid CustomerId,
    decimal Amount,
    string Currency,
    PaymentStatus Status,
    string GatewayProvider,
    string? GatewayTransactionId,
    int Version)
{
    public bool CanBeRefunded => Status == PaymentStatus.Captured;
    public bool IsAlreadyRefunded => Status == PaymentStatus.Refunded;
}
