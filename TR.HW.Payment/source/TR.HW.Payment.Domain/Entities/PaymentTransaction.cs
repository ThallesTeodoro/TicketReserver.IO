namespace TR.HW.Payment.Domain.Entities;

using TR.HW.Payment.Domain.Enums;
using TR.HW.Payment.Domain.Exceptions;

public sealed class PaymentTransaction
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

    // Construtor privado para ORMs / Dapper
    private PaymentTransaction() { }

    internal PaymentTransaction(
        Guid id,
        Guid paymentId,
        TransactionType transactionType,
        decimal amount,
        TransactionStatus status,
        string? gatewayReference = null,
        string? requestPayload = null,
        string? responsePayload = null,
        string? errorDetails = null,
        DateTime? createdAt = null)
    {
        if (amount <= 0)
        {
            throw new PaymentDomainException("O valor da transação deve ser estritamente maior que zero.");
        }

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        PaymentId = paymentId;
        TransactionType = transactionType;
        Amount = amount;
        Status = status;
        GatewayReference = gatewayReference;
        RequestPayload = requestPayload;
        ResponsePayload = responsePayload;
        ErrorDetails = errorDetails;
        CreatedAt = createdAt ?? DateTime.UtcNow;
    }
}
