namespace TR.API.Reservation.Infrastructure.Caching;

using TR.API.Reservation.Domain.Contracts.Caching;

public interface IRedisHoldService : IHoldCacheService
{
    Task<bool> AcquireHoldAsync(Guid eventId, Guid ticketTierId, Guid? seatId, Guid customerId, TimeSpan duration, CancellationToken cancellationToken = default);
    Task<bool> ReleaseHoldAsync(Guid eventId, Guid ticketTierId, Guid? seatId, Guid customerId, CancellationToken cancellationToken = default);
}
