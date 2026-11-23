namespace TR.HW.ResCompensator.Infrastructure.Caching;

using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using TR.HW.ResCompensator.Application.Interfaces;

public sealed class RedisSeatLockReleaser(
    IConnectionMultiplexer redis,
    ILogger<RedisSeatLockReleaser> logger) : IRedisSeatLockReleaser
{
    private const string ReleaseLockLuaScript = """
        if redis.call("get", KEYS[1]) == ARGV[1] then
            return redis.call("del", KEYS[1])
        else
            return 0
        end
        """;

    private readonly IDatabase _database = redis.GetDatabase();

    public async Task<int> ReleaseHeldSeatsAsync(
        Guid eventId,
        Guid reservationId,
        IEnumerable<Guid> seatIds,
        CancellationToken ct = default)
    {
        var reservationIdStr = reservationId.ToString();
        var releasedCount = 0;

        foreach (var seatId in seatIds)
        {
            var key = $"hold:event:{eventId}:seat:{seatId}";
            try
            {
                var result = (int)await _database.ScriptEvaluateAsync(
                    ReleaseLockLuaScript,
                    [new RedisKey(key)],
                    [new RedisValue(reservationIdStr)]);

                if (result == 1)
                {
                    releasedCount++;
                    logger.LogInformation(
                        "Released Redis seat lock for key {Key} owned by Reservation {ReservationId}",
                        key,
                        reservationId);
                }
                else
                {
                    logger.LogDebug(
                        "Redis seat lock for key {Key} was not deleted (owned by another reservation or already expired)",
                        key);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed to execute Lua seat release script on Redis for key {Key} (Reservation {ReservationId})",
                    key,
                    reservationId);
            }
        }

        return releasedCount;
    }
}
