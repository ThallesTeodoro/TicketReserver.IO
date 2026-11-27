namespace TR.HW.Refund.Domain.Entities;

using TR.HW.Refund.Domain.Enums;
using TR.HW.Refund.Domain.Exceptions;

public sealed class RefundTransaction
{
    public Guid Id { get; private set; }
    public Guid PaymentId { get; private set; }
    public TransactionType TransactionType { get; private set; }
    public decimal Amount { get; private set; }
    public TransactionStatus Status { get; private set; }
    public string? GatewayReference { get; private set; }
    public string? RequestPayload { get; private set; }
    public string? ResponsePayload { get; private set; }
    public string? ErrorDetails { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private RefundTransaction() { }

    public static RefundTransaction CreateSuccess(
        Guid paymentId,
        decimal amount,
        string gatewayReference,
        string? requestPayload = null,
        string? responsePayload = null)
    {
        if (paymentId == Guid.Empty)
        {
            throw new DomainException("PaymentId cannot be empty.");
        }

        if (amount <= 0)
        {
            throw new DomainException("Transaction amount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(gatewayReference))
        {
            throw new DomainException("GatewayReference cannot be empty for successful refund transaction.");
        }

        return new RefundTransaction
        {
            Id = Guid.NewGuid(),
            PaymentId = paymentId,
            TransactionType = TransactionType.Refund,
            Amount = amount,
            Status = TransactionStatus.Success,
            GatewayReference = gatewayReference.Trim(),
            RequestPayload = requestPayload,
            ResponsePayload = responsePayload,
            CreatedAt = DateTime.UtcNow
        };
    }

    public static RefundTransaction CreateFailure(
        Guid paymentId,
        decimal amount,
        string errorDetails,
        string? requestPayload = null,
        string? responsePayload = null)
    {
        if (paymentId == Guid.Empty)
        {
            throw new DomainException("PaymentId cannot be empty.");
        }

        if (amount <= 0)
        {
            throw new DomainException("Transaction amount must be greater than zero.");
        }

        return new RefundTransaction
        {
            Id = Guid.NewGuid(),
            PaymentId = paymentId,
            TransactionType = TransactionType.Refund,
            Amount = amount,
            Status = TransactionStatus.Failed,
            ErrorDetails = errorDetails,
            RequestPayload = requestPayload,
            ResponsePayload = responsePayload,
            CreatedAt = DateTime.UtcNow
        };
    }
}
