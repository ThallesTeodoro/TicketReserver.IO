namespace TR.API.Reservation.Infrastructure.Caching;

public interface IRedisHoldService
{
    Task<bool> AcquireHoldAsync(Guid eventId, Guid ticketTierId, Guid? seatId, Guid customerId, TimeSpan duration, CancellationToken cancellationToken = default);
    Task<bool> ReleaseHoldAsync(Guid eventId, Guid ticketTierId, Guid? seatId, Guid customerId, CancellationToken cancellationToken = default);
    Task<bool> IsHeldAsync(Guid eventId, Guid ticketTierId, Guid? seatId, CancellationToken cancellationToken = default);
}
