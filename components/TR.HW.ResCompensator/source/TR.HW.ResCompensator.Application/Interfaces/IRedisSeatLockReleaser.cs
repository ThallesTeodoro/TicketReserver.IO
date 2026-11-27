namespace TR.HW.ResCompensator.Application.Interfaces;

public interface IRedisSeatLockReleaser
{
    Task<int> ReleaseHeldSeatsAsync(
        Guid eventId,
        Guid reservationId,
        IEnumerable<Guid> seatIds,
        CancellationToken ct = default);
}
