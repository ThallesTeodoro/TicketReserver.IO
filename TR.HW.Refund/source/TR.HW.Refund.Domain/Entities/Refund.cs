namespace TR.HW.Refund.Domain.Entities;

using TR.HW.Refund.Domain.Enums;
using TR.HW.Refund.Domain.Events;
using TR.HW.Refund.Domain.Exceptions;

public sealed class Refund
{
    private readonly List<RefundTransaction> _transactions = new();
    private readonly List<IDomainEvent> _domainEvents = new();

    public Guid Id { get; private set; }
    public Guid PaymentId { get; private set; }
    public Guid ReservationId { get; private set; }
    public Guid CustomerId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = "BRL";
    public string Reason { get; private set; } = string.Empty;
    public RefundStatus Status { get; private set; }
    public string? GatewayRefundId { get; private set; }
    public string? GatewayResponse { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public IReadOnlyCollection<RefundTransaction> Transactions => _transactions.AsReadOnly();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    private Refund() { }

    public static Refund CreatePending(
        Guid paymentId,
        Guid reservationId,
        Guid customerId,
        decimal amount,
        string currency,
        string reason)
    {
        if (paymentId == Guid.Empty)
        {
            throw new DomainException("PaymentId cannot be empty.");
        }

        if (reservationId == Guid.Empty)
        {
            throw new DomainException("ReservationId cannot be empty.");
        }

        if (customerId == Guid.Empty)
        {
            throw new DomainException("CustomerId cannot be empty.");
        }

        if (amount <= 0)
        {
            throw new DomainException("Refund amount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
        {
            throw new DomainException("Currency must be a valid 3-letter ISO code.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("Refund reason cannot be empty.");
        }

        var now = DateTime.UtcNow;

        return new Refund
        {
            Id = Guid.NewGuid(),
            PaymentId = paymentId,
            ReservationId = reservationId,
            CustomerId = customerId,
            Amount = amount,
            Currency = currency.Trim().ToUpperInvariant(),
            Reason = reason.Trim(),
            Status = RefundStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Complete(
        string gatewayRefundId,
        string? requestPayload = null,
        string? responsePayload = null)
    {
        if (Status != RefundStatus.Pending)
        {
            throw new DomainException($"Cannot complete a refund that is in status '{Status}'.");
        }

        if (string.IsNullOrWhiteSpace(gatewayRefundId))
        {
            throw new DomainException("GatewayRefundId cannot be empty when completing a refund.");
        }

        var now = DateTime.UtcNow;
        Status = RefundStatus.Completed;
        GatewayRefundId = gatewayRefundId.Trim();
        GatewayResponse = responsePayload;
        UpdatedAt = now;

        _transactions.Add(RefundTransaction.CreateSuccess(
            PaymentId,
            Amount,
            GatewayRefundId,
            requestPayload,
            responsePayload));

        _domainEvents.Add(new PaymentRefundedDomainEvent(
            RefundId: Id,
            PaymentId: PaymentId,
            ReservationId: ReservationId,
            CustomerId: CustomerId,
            Amount: Amount,
            Currency: Currency,
            GatewayRefundId: GatewayRefundId,
            Reason: Reason,
            OccurredOn: now));
    }

    public void Fail(
        string errorDetails,
        string? requestPayload = null,
        string? responsePayload = null)
    {
        if (Status != RefundStatus.Pending)
        {
            throw new DomainException($"Cannot fail a refund that is in status '{Status}'.");
        }

        if (string.IsNullOrWhiteSpace(errorDetails))
        {
            throw new DomainException("ErrorDetails cannot be empty when failing a refund.");
        }

        var now = DateTime.UtcNow;
        Status = RefundStatus.Failed;
        GatewayResponse = responsePayload;
        UpdatedAt = now;

        _transactions.Add(RefundTransaction.CreateFailure(
            PaymentId,
            Amount,
            errorDetails.Trim(),
            requestPayload,
            responsePayload));
    }

    public void ClearDomainEvents() => _domainEvents.Clear();
}
