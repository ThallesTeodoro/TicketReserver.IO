namespace TR.HW.Ticketing.Domain.Entities;

using TR.HW.Ticketing.Domain.Enums;
using TR.HW.Ticketing.Domain.Events;
using TR.HW.Ticketing.Domain.Exceptions;
using TR.HW.Ticketing.Domain.Services;

public sealed class Ticket
{
    private readonly List<TicketCheckin> _checkins = new();
    private readonly List<IDomainEvent> _domainEvents = new();

    public Guid Id { get; private set; }
    public string TicketCode { get; private set; } = string.Empty;
    public string QrCodeHash { get; private set; } = string.Empty;
    public Guid ReservationId { get; private set; }
    public Guid PaymentId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid EventId { get; private set; }
    public Guid TicketTierId { get; private set; }
    public Guid? SeatId { get; private set; }
    public string? SeatLabel { get; private set; }
    public TicketStatus Status { get; private set; }
    public decimal PricePaid { get; private set; }
    public DateTime IssuedAt { get; private set; }
    public DateTime? CheckedInAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? RevocationReason { get; private set; }
    public int Version { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public IReadOnlyCollection<TicketCheckin> Checkins => _checkins.AsReadOnly();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    private Ticket() { }

    public static Ticket Issue(
        Guid reservationId,
        Guid paymentId,
        Guid customerId,
        Guid eventId,
        Guid ticketTierId,
        decimal pricePaid,
        IQrCodeGenerator qrCodeGenerator,
        Guid? seatId = null,
        string? seatLabel = null)
    {
        if (reservationId == Guid.Empty)
        {
            throw new DomainException("ReservationId cannot be empty.");
        }

        if (paymentId == Guid.Empty)
        {
            throw new DomainException("PaymentId cannot be empty.");
        }

        if (customerId == Guid.Empty)
        {
            throw new DomainException("CustomerId cannot be empty.");
        }

        if (eventId == Guid.Empty)
        {
            throw new DomainException("EventId cannot be empty.");
        }

        if (ticketTierId == Guid.Empty)
        {
            throw new DomainException("TicketTierId cannot be empty.");
        }

        if (pricePaid < 0)
        {
            throw new DomainException("PricePaid cannot be negative.");
        }

        ArgumentNullException.ThrowIfNull(qrCodeGenerator);

        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var ticketCode = $"TKT-{now.Year}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        var qrCodeHash = qrCodeGenerator.GenerateSignature(id, eventId, customerId, now);

        if (string.IsNullOrWhiteSpace(qrCodeHash))
        {
            throw new DomainException("Generated QR code hash cannot be empty.");
        }

        var ticket = new Ticket
        {
            Id = id,
            TicketCode = ticketCode,
            QrCodeHash = qrCodeHash,
            ReservationId = reservationId,
            PaymentId = paymentId,
            CustomerId = customerId,
            EventId = eventId,
            TicketTierId = ticketTierId,
            SeatId = seatId,
            SeatLabel = seatLabel?.Trim(),
            Status = TicketStatus.Active,
            PricePaid = pricePaid,
            IssuedAt = now,
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now
        };

        ticket._domainEvents.Add(new TicketIssuedDomainEvent(
            TicketId: ticket.Id,
            TicketCode: ticket.TicketCode,
            QrCodeHash: ticket.QrCodeHash,
            ReservationId: ticket.ReservationId,
            PaymentId: ticket.PaymentId,
            CustomerId: ticket.CustomerId,
            EventId: ticket.EventId,
            TicketTierId: ticket.TicketTierId,
            SeatId: ticket.SeatId,
            SeatLabel: ticket.SeatLabel,
            PricePaid: ticket.PricePaid,
            OccurredOn: now));

        return ticket;
    }

    public SeatAllocation CreateSeatAllocation()
    {
        var effectiveSeatId = SeatId ?? Id;
        var allocationType = SeatId.HasValue ? AllocationType.NumberedSeat : AllocationType.GeneralAdmission;
        return SeatAllocation.Create(EventId, effectiveSeatId, ReservationId, Id, allocationType);
    }

    public void CheckIn(string gateName, string deviceId)
    {
        if (string.IsNullOrWhiteSpace(gateName))
        {
            throw new DomainException("GateName cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(deviceId))
        {
            throw new DomainException("DeviceId cannot be empty.");
        }

        if (Status == TicketStatus.Used)
        {
            _checkins.Add(TicketCheckin.Create(Id, gateName, deviceId, CheckinStatus.AlreadyUsed, "Ticket already used."));
            throw new DomainException("Ticket has already been used.");
        }

        if (Status == TicketStatus.Revoked || Status == TicketStatus.Blocked)
        {
            _checkins.Add(TicketCheckin.Create(Id, gateName, deviceId, CheckinStatus.RevokedTicket, $"Ticket status is {Status}."));
            throw new DomainException($"Cannot check in ticket with status {Status}.");
        }

        var now = DateTime.UtcNow;
        Status = TicketStatus.Used;
        CheckedInAt = now;
        UpdatedAt = now;
        Version++;

        _checkins.Add(TicketCheckin.Create(Id, gateName, deviceId, CheckinStatus.Approved, "Check-in approved."));
    }

    public void Revoke(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("Revocation reason cannot be empty.");
        }

        if (Status == TicketStatus.Used)
        {
            throw new DomainException("Cannot revoke an already used ticket.");
        }

        if (Status == TicketStatus.Revoked)
        {
            throw new DomainException("Ticket is already revoked.");
        }

        var now = DateTime.UtcNow;
        Status = TicketStatus.Revoked;
        RevokedAt = now;
        RevocationReason = reason.Trim();
        UpdatedAt = now;
        Version++;
    }

    public void ClearDomainEvents() => _domainEvents.Clear();
}
