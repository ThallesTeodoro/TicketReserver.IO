namespace TR.HW.OutboxRelay.Domain.Events;

/// <summary>
/// Evento disparado quando uma nova reserva é criada.
/// </summary>
public record ReservationCreated(
    Guid ReservationId,
    Guid UserId,
    Guid EventId,
    string SeatNumber,
    decimal TicketPrice,
    DateTime CreatedAt) : IEvent
{
    public Guid Id => ReservationId;
    public DateTime OccurredAt => CreatedAt;
}
