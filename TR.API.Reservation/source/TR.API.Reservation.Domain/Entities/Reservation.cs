using TR.API.Reservation.Domain.Enums;
using TR.API.Reservation.Domain.Exceptions;

namespace TR.API.Reservation.Domain.Entities;

public class Reservation
{
    private readonly List<ReservationItem> _items = new();

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid EventId { get; private set; }
    public ReservationStatus Status { get; private set; }
    public decimal TotalAmount { get; private set; }
    public string Currency { get; private set; } = "BRL";
    public DateTime HoldExpiresAt { get; private set; }
    public DateTime? PaymentAttemptedAt { get; private set; }
    public DateTime? ConfirmedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }
    public int Version { get; private set; } = 1;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public IReadOnlyCollection<ReservationItem> Items => _items.AsReadOnly();

    // Construtor protegido para o EF Core
    protected Reservation() { }

    private Reservation(
        Guid id,
        Guid customerId,
        Guid eventId,
        TimeSpan holdDuration,
        string currency = "BRL")
    {
        if (customerId == Guid.Empty)
            throw new DomainException("CustomerId não pode ser vazio.");

        if (eventId == Guid.Empty)
            throw new DomainException("EventId não pode ser vazio.");

        if (holdDuration <= TimeSpan.Zero)
            throw new DomainException("A duração do hold temporário deve ser maior que zero.");

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        CustomerId = customerId;
        EventId = eventId;
        Status = ReservationStatus.AWAITING_PAYMENT;
        Currency = string.IsNullOrWhiteSpace(currency) ? "BRL" : currency.Trim().ToUpperInvariant();
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
        HoldExpiresAt = CreatedAt.Add(holdDuration);
        TotalAmount = 0m;
        Version = 1;
    }

    public static Reservation Create(
        Guid customerId,
        Guid eventId,
        TimeSpan holdDuration,
        string currency = "BRL")
    {
        return new Reservation(Guid.NewGuid(), customerId, eventId, holdDuration, currency);
    }

    public void AddItem(
        Guid ticketTierId,
        Guid? seatId,
        string? seatLabel,
        int quantity,
        decimal unitPrice)
    {
        if (Status != ReservationStatus.AWAITING_PAYMENT)
            throw new DomainException("Não é permitido adicionar itens a uma reserva que não esteja aguardando pagamento.");

        var item = ReservationItem.Create(Id, ticketTierId, seatId, seatLabel, quantity, unitPrice);
        _items.Add(item);

        RecalculateTotal();
        UpdatedAt = DateTime.UtcNow;
    }

    public void StartPayment(DateTime now)
    {
        if (Status != ReservationStatus.AWAITING_PAYMENT)
            throw new ConflictException($"Não é possível iniciar pagamento para uma reserva com status '{Status}'.");

        if (now >= HoldExpiresAt)
        {
            Expire(now);
            throw new ConflictException("O tempo de reserva temporária (hold) expirou.");
        }

        if (!_items.Any())
            throw new DomainException("A reserva não possui itens vinculados.");

        Status = ReservationStatus.PROCESSING_PAYMENT;
        PaymentAttemptedAt = now;
        UpdatedAt = now;
        Version++;
    }

    public void ConfirmPayment(DateTime confirmedAt)
    {
        if (Status != ReservationStatus.PROCESSING_PAYMENT && Status != ReservationStatus.AWAITING_PAYMENT)
            throw new ConflictException($"Não é possível confirmar uma reserva com status '{Status}'.");

        Status = ReservationStatus.CONFIRMED;
        ConfirmedAt = confirmedAt;
        UpdatedAt = confirmedAt;
        Version++;
    }

    public void Expire(DateTime now)
    {
        if (Status == ReservationStatus.CONFIRMED)
            throw new ConflictException("Uma reserva confirmada não pode ser expirada.");

        if (Status == ReservationStatus.CANCELLED)
            return;

        Status = ReservationStatus.EXPIRED;
        UpdatedAt = now;
        Version++;
    }

    public void Cancel(string reason, DateTime now)
    {
        if (Status == ReservationStatus.CONFIRMED)
            throw new ConflictException("Uma reserva já confirmada deve ser compensada via estorno, não cancelada diretamente.");

        Status = ReservationStatus.CANCELLED;
        CancellationReason = string.IsNullOrWhiteSpace(reason) ? "Cancelado pelo usuário ou sistema" : reason.Trim();
        CancelledAt = now;
        UpdatedAt = now;
        Version++;
    }

    private void RecalculateTotal()
    {
        TotalAmount = Math.Round(_items.Sum(i => i.Subtotal), 2);
    }
}
