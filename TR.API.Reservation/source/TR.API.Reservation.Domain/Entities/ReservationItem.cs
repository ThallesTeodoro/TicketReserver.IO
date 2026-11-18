using TR.API.Reservation.Domain.Exceptions;

namespace TR.API.Reservation.Domain.Entities;

public class ReservationItem
{
    public Guid Id { get; private set; }
    public Guid ReservationId { get; private set; }
    public Guid TicketTierId { get; private set; }
    public Guid? SeatId { get; private set; }
    public string? SeatLabel { get; private set; }
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal Subtotal { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // Construtor protegido para o EF Core
    protected ReservationItem() { }

    private ReservationItem(
        Guid id,
        Guid reservationId,
        Guid ticketTierId,
        Guid? seatId,
        string? seatLabel,
        int quantity,
        decimal unitPrice)
    {
        if (reservationId == Guid.Empty)
            throw new DomainException("ReservationId não pode ser vazio.");

        if (ticketTierId == Guid.Empty)
            throw new DomainException("TicketTierId não pode ser vazio.");

        if (quantity <= 0)
            throw new DomainException("A quantidade do item deve ser maior que zero.");

        if (unitPrice < 0)
            throw new DomainException("O preço unitário não pode ser negativo.");

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        ReservationId = reservationId;
        TicketTierId = ticketTierId;
        SeatId = seatId;
        SeatLabel = string.IsNullOrWhiteSpace(seatLabel) ? null : seatLabel.Trim();
        Quantity = quantity;
        UnitPrice = unitPrice;
        Subtotal = Math.Round(unitPrice * quantity, 2);
        CreatedAt = DateTime.UtcNow;
    }

    public static ReservationItem Create(
        Guid reservationId,
        Guid ticketTierId,
        Guid? seatId,
        string? seatLabel,
        int quantity,
        decimal unitPrice)
    {
        return new ReservationItem(
            Guid.NewGuid(),
            reservationId,
            ticketTierId,
            seatId,
            seatLabel,
            quantity,
            unitPrice);
    }
}
