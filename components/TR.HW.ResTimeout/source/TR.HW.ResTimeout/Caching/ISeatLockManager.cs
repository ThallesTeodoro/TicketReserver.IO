namespace TR.HW.ResTimeout.Caching;

public interface ISeatLockManager
{
    Task<int> ReleaseSeatLocksAsync(Guid eventId, IEnumerable<Guid> seatIds, Guid reservationId, CancellationToken ct = default);
}
