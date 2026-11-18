namespace TR.API.Reservation.Infrastructure.Caching;

using StackExchange.Redis;
using TR.API.Reservation.Domain.Contracts.Caching;

public sealed class RedisHoldService(IConnectionMultiplexer redis) : IRedisHoldService
{
    private readonly IDatabase _database = redis.GetDatabase();

    private const string SafeReleaseScript = @"
        if redis.call('get', KEYS[1]) == ARGV[1] then
            return redis.call('del', KEYS[1])
        else
            return 0
        end";

    private const string DeductQuotaScript = @"
        local current = tonumber(redis.call('get', KEYS[1]))
        if current == nil then
            return 1
        end
        if current >= tonumber(ARGV[1]) then
            redis.call('decrby', KEYS[1], ARGV[1])
            return 1
        else
            return 0
        end";

    public async Task<bool> AcquireSeatHoldAsync(
        Guid eventId,
        Guid seatId,
        Guid reservationId,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        var key = BuildSeatKey(eventId, seatId);
        return await _database.StringSetAsync(key, reservationId.ToString(), duration, When.NotExists);
    }

    public async Task<bool> ReleaseSeatHoldAsync(
        Guid eventId,
        Guid seatId,
        Guid reservationId,
        CancellationToken cancellationToken = default)
    {
        var key = BuildSeatKey(eventId, seatId);
        var result = await _database.ScriptEvaluateAsync(
            SafeReleaseScript,
            new RedisKey[] { key },
            new RedisValue[] { reservationId.ToString() });

        return (int)result == 1;
    }

    public async Task<bool> AcquireTierQuotaHoldAsync(
        Guid eventId,
        Guid ticketTierId,
        int quantity,
        Guid reservationId,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        var quotaKey = BuildTierQuotaKey(eventId, ticketTierId);
        var result = await _database.ScriptEvaluateAsync(
            DeductQuotaScript,
            new RedisKey[] { quotaKey },
            new RedisValue[] { quantity });

        return (int)result == 1;
    }

    public async Task<bool> ReleaseTierQuotaHoldAsync(
        Guid eventId,
        Guid ticketTierId,
        int quantity,
        CancellationToken cancellationToken = default)
    {
        var quotaKey = BuildTierQuotaKey(eventId, ticketTierId);
        var exists = await _database.KeyExistsAsync(quotaKey);
        if (exists)
        {
            await _database.StringIncrementAsync(quotaKey, quantity);
        }
        return true;
    }

    public async Task SetTierQuotaAsync(
        Guid eventId,
        Guid ticketTierId,
        int totalQuota,
        CancellationToken cancellationToken = default)
    {
        var quotaKey = BuildTierQuotaKey(eventId, ticketTierId);
        await _database.StringSetAsync(quotaKey, totalQuota);
    }

    public async Task ReleaseReservationHoldsAsync(
        Guid eventId,
        Guid reservationId,
        IEnumerable<HoldItemDescriptor> items,
        CancellationToken cancellationToken = default)
    {
        foreach (var item in items)
        {
            if (item.SeatId.HasValue)
            {
                await ReleaseSeatHoldAsync(eventId, item.SeatId.Value, reservationId, cancellationToken);
            }
            else
            {
                await ReleaseTierQuotaHoldAsync(eventId, item.TicketTierId, item.Quantity, cancellationToken);
            }
        }
    }

    public async Task<bool> IsHeldAsync(
        Guid eventId,
        Guid ticketTierId,
        Guid? seatId,
        CancellationToken cancellationToken = default)
    {
        var key = seatId.HasValue
            ? BuildSeatKey(eventId, seatId.Value)
            : BuildTierQuotaKey(eventId, ticketTierId);

        return await _database.KeyExistsAsync(key);
    }

    public async Task<bool> AcquireHoldAsync(
        Guid eventId,
        Guid ticketTierId,
        Guid? seatId,
        Guid customerId,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        if (seatId.HasValue)
        {
            return await AcquireSeatHoldAsync(eventId, seatId.Value, customerId, duration, cancellationToken);
        }

        return await AcquireTierQuotaHoldAsync(eventId, ticketTierId, 1, customerId, duration, cancellationToken);
    }

    public async Task<bool> ReleaseHoldAsync(
        Guid eventId,
        Guid ticketTierId,
        Guid? seatId,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        if (seatId.HasValue)
        {
            return await ReleaseSeatHoldAsync(eventId, seatId.Value, customerId, cancellationToken);
        }

        return await ReleaseTierQuotaHoldAsync(eventId, ticketTierId, 1, cancellationToken);
    }

    private static string BuildSeatKey(Guid eventId, Guid seatId) =>
        $"hold:event:{eventId}:seat:{seatId}";

    private static string BuildTierQuotaKey(Guid eventId, Guid ticketTierId) =>
        $"hold:event:{eventId}:tier:{ticketTierId}:quota";
}
