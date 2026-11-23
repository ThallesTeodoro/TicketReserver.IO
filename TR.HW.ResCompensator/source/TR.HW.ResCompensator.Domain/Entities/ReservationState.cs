namespace TR.HW.ResCompensator.Domain.Entities;

using TR.HW.ResCompensator.Domain.Enums;
using TR.HW.ResCompensator.Domain.Exceptions;

public sealed class ReservationState
{
    private readonly List<ReservedSeatItem> _items = new();

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid EventId { get; private set; }
    public ReservationStatus Status { get; private set; }
    public decimal TotalAmount { get; private set; }
    public string Currency { get; private set; } = "BRL";
    public DateTime? ConfirmedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }
    public int Version { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public IReadOnlyCollection<ReservedSeatItem> Items => _items.AsReadOnly();

    private ReservationState() { }

    public static ReservationState Reconstitute(
        Guid id,
        Guid customerId,
        Guid eventId,
        ReservationStatus status,
        decimal totalAmount,
        string currency,
        int version,
        IEnumerable<ReservedSeatItem>? items = null,
        DateTime? confirmedAt = null,
        DateTime? cancelledAt = null,
        string? cancellationReason = null,
        DateTime? updatedAt = null)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("Reservation Id cannot be empty.");
        }

        if (customerId == Guid.Empty)
        {
            throw new DomainException("CustomerId cannot be empty.");
        }

        if (eventId == Guid.Empty)
        {
            throw new DomainException("EventId cannot be empty.");
        }

        var reservation = new ReservationState
        {
            Id = id,
            CustomerId = customerId,
            EventId = eventId,
            Status = status,
            TotalAmount = totalAmount,
            Currency = string.IsNullOrWhiteSpace(currency) ? "BRL" : currency.Trim().ToUpperInvariant(),
            ConfirmedAt = confirmedAt,
            CancelledAt = cancelledAt,
            CancellationReason = cancellationReason,
            Version = version,
            UpdatedAt = updatedAt ?? DateTime.UtcNow
        };

        if (items is not null)
        {
            reservation._items.AddRange(items);
        }

        return reservation;
    }

    public bool Confirm()
    {
        if (Status == ReservationStatus.Confirmed)
        {
            return false;
        }

        if (Status == ReservationStatus.Cancelled || Status == ReservationStatus.Expired)
        {
            throw new DomainException($"Cannot confirm reservation in terminal status '{Status}'.");
        }

        var now = DateTime.UtcNow;
        Status = ReservationStatus.Confirmed;
        ConfirmedAt = now;
        UpdatedAt = now;
        Version++;
        return true;
    }

    public bool Cancel(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("Cancellation reason cannot be empty.");
        }

        if (Status == ReservationStatus.Cancelled || Status == ReservationStatus.Expired)
        {
            return false;
        }

        var now = DateTime.UtcNow;
        Status = ReservationStatus.Cancelled;
        CancelledAt = now;
        CancellationReason = reason.Trim();
        UpdatedAt = now;
        Version++;
        return true;
    }
}
