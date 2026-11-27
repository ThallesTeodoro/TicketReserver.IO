namespace TR.HW.ResTimeout.Caching;

using Microsoft.Extensions.Logging;
using StackExchange.Redis;

public sealed class SeatLockManager(IConnectionMultiplexer redis, ILogger<SeatLockManager> logger) : ISeatLockManager
{
    private const string ReleaseLockScript = """
        if redis.call("get", KEYS[1]) == ARGV[1] then
            return redis.call("del", KEYS[1])
        else
            return 0
        end
        """;

    private readonly IDatabase _database = redis.GetDatabase();

    public async Task<int> ReleaseSeatLocksAsync(Guid eventId, IEnumerable<Guid> seatIds, Guid reservationId, CancellationToken ct = default)
    {
        var reservationIdStr = reservationId.ToString();
        var releasedCount = 0;

        foreach (var seatId in seatIds)
        {
            var key = $"hold:event:{eventId}:seat:{seatId}";
            try
            {
                var result = (int)await _database.ScriptEvaluateAsync(
                    ReleaseLockScript,
                    [new RedisKey(key)],
                    [new RedisValue(reservationIdStr)]);

                if (result == 1)
                {
                    releasedCount++;
                    logger.LogInformation("Released seat lock for key {Key} and reservation {ReservationId}", key, reservationId);
                }
                else
                {
                    logger.LogWarning("Seat lock for key {Key} was not released (not matching reservation {ReservationId} or already expired)", key, reservationId);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to evaluate Lua release script on Redis for key {Key}", key);
            }
        }

        return releasedCount;
    }
}
