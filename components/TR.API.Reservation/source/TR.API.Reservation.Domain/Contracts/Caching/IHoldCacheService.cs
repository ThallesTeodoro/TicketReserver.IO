namespace TR.API.Reservation.Domain.Contracts.Caching;

public record HoldItemDescriptor(Guid TicketTierId, Guid? SeatId, int Quantity);

public interface IHoldCacheService
{
    Task<bool> AcquireSeatHoldAsync(Guid eventId, Guid seatId, Guid reservationId, TimeSpan duration, CancellationToken cancellationToken = default);
    Task<bool> ReleaseSeatHoldAsync(Guid eventId, Guid seatId, Guid reservationId, CancellationToken cancellationToken = default);
    Task<bool> AcquireTierQuotaHoldAsync(Guid eventId, Guid ticketTierId, int quantity, Guid reservationId, TimeSpan duration, CancellationToken cancellationToken = default);
    Task<bool> ReleaseTierQuotaHoldAsync(Guid eventId, Guid ticketTierId, int quantity, CancellationToken cancellationToken = default);
    Task SetTierQuotaAsync(Guid eventId, Guid ticketTierId, int totalQuota, CancellationToken cancellationToken = default);
    Task ReleaseReservationHoldsAsync(Guid eventId, Guid reservationId, IEnumerable<HoldItemDescriptor> items, CancellationToken cancellationToken = default);
    Task<bool> IsHeldAsync(Guid eventId, Guid ticketTierId, Guid? seatId, CancellationToken cancellationToken = default);
}
