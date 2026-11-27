namespace TR.HW.Payment.Domain.Entities;

using TR.HW.Payment.Domain.Enums;
using TR.HW.Payment.Domain.Events;
using TR.HW.Payment.Domain.Exceptions;

public sealed class Payment
{
    private readonly List<PaymentTransaction> _transactions = [];
    private readonly List<IDomainEvent> _domainEvents = [];

    public Guid Id { get; private set; }
    public Guid ReservationId { get; private set; }
    public Guid CustomerId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = "BRL";
    public PaymentMethod PaymentMethod { get; private set; }
    public PaymentStatus Status { get; private set; }
    public string GatewayProvider { get; private set; } = "STRIPE";
    public string? GatewayTransactionId { get; private set; }
    public string? AuthorizationCode { get; private set; }
    public string? CardLastFour { get; private set; }
    public string? CardBrand { get; private set; }
    public string? FailureReason { get; private set; }
    public int Version { get; private set; } = 1;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public IReadOnlyCollection<PaymentTransaction> Transactions => _transactions.AsReadOnly();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    private Payment() { }

    public static Payment CreatePending(
        Guid reservationId,
        Guid customerId,
        decimal amount,
        PaymentMethod paymentMethod = PaymentMethod.CreditCard,
        string currency = "BRL",
        string gatewayProvider = "STRIPE",
        string? cardLastFour = null,
        string? cardBrand = null)
    {
        if (reservationId == Guid.Empty)
        {
            throw new PaymentDomainException("O identificador da reserva (ReservationId) é obrigatório.");
        }

        if (customerId == Guid.Empty)
        {
            throw new PaymentDomainException("O identificador do cliente (CustomerId) é obrigatório.");
        }

        if (amount <= 0)
        {
            throw new PaymentDomainException("O montante do pagamento deve ser estritamente maior que zero.");
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new PaymentDomainException("A moeda do pagamento deve ser informada.");
        }

        var now = DateTime.UtcNow;
        return new Payment
        {
            Id = Guid.NewGuid(),
            ReservationId = reservationId,
            CustomerId = customerId,
            Amount = amount,
            Currency = currency.ToUpperInvariant(),
            PaymentMethod = paymentMethod,
            Status = PaymentStatus.Pending,
            GatewayProvider = gatewayProvider,
            CardLastFour = cardLastFour,
            CardBrand = cardBrand,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Capture(
        string gatewayTransactionId,
        string authorizationCode,
        string? requestPayload = null,
        string? responsePayload = null)
    {
        if (Status != PaymentStatus.Pending && Status != PaymentStatus.Authorized)
        {
            throw new PaymentDomainException($"Não é possível capturar pagamento com status atual '{Status}'.");
        }

        if (string.IsNullOrWhiteSpace(gatewayTransactionId))
        {
            throw new PaymentDomainException("O identificador da transação no gateway é obrigatório para captura.");
        }

        if (string.IsNullOrWhiteSpace(authorizationCode))
        {
            throw new PaymentDomainException("O código de autorização é obrigatório para captura.");
        }

        GatewayTransactionId = gatewayTransactionId;
        AuthorizationCode = authorizationCode;
        Status = PaymentStatus.Captured;
        UpdatedAt = DateTime.UtcNow;

        var transaction = new PaymentTransaction(
            id: Guid.NewGuid(),
            paymentId: Id,
            transactionType: TransactionType.Capture,
            amount: Amount,
            status: TransactionStatus.Success,
            gatewayReference: gatewayTransactionId,
            requestPayload: requestPayload,
            responsePayload: responsePayload);

        _transactions.Add(transaction);

        _domainEvents.Add(new PaymentApprovedDomainEvent(
            PaymentId: Id,
            ReservationId: ReservationId,
            CustomerId: CustomerId,
            Amount: Amount,
            Currency: Currency,
            GatewayTransactionId: gatewayTransactionId,
            AuthorizationCode: authorizationCode,
            OccurredOn: UpdatedAt));
    }

    public void Decline(
        string failureReason,
        string? requestPayload = null,
        string? responsePayload = null)
    {
        if (Status != PaymentStatus.Pending)
        {
            throw new PaymentDomainException($"Não é possível recusar pagamento com status atual '{Status}'.");
        }

        FailureReason = string.IsNullOrWhiteSpace(failureReason) ? "Recusado pelo gateway" : failureReason;
        Status = PaymentStatus.Declined;
        UpdatedAt = DateTime.UtcNow;

        var transaction = new PaymentTransaction(
            id: Guid.NewGuid(),
            paymentId: Id,
            transactionType: TransactionType.Authorize,
            amount: Amount,
            status: TransactionStatus.Failed,
            gatewayReference: null,
            requestPayload: requestPayload,
            responsePayload: responsePayload,
            errorDetails: FailureReason);

        _transactions.Add(transaction);

        _domainEvents.Add(new PaymentDeclinedDomainEvent(
            PaymentId: Id,
            ReservationId: ReservationId,
            CustomerId: CustomerId,
            Amount: Amount,
            FailureReason: FailureReason,
            OccurredOn: UpdatedAt));
    }

    public void ClearDomainEvents() => _domainEvents.Clear();
}
