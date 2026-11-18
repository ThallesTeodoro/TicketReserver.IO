namespace TR.API.Reservation.Infrastructure.Caching;

using StackExchange.Redis;

public sealed class RedisHoldService(IConnectionMultiplexer redis) : IRedisHoldService
{
    private readonly IDatabase _database = redis.GetDatabase();

    public async Task<bool> AcquireHoldAsync(
        Guid eventId,
        Guid ticketTierId,
        Guid? seatId,
        Guid customerId,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        var key = BuildKey(eventId, ticketTierId, seatId);
        var value = customerId.ToString();

        return await _database.StringSetAsync(key, value, duration, When.NotExists);
    }

    public async Task<bool> ReleaseHoldAsync(
        Guid eventId,
        Guid ticketTierId,
        Guid? seatId,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var key = BuildKey(eventId, ticketTierId, seatId);
        var existing = await _database.StringGetAsync(key);

        if (!existing.HasValue || existing != customerId.ToString())
        {
            return false;
        }

        return await _database.KeyDeleteAsync(key);
    }

    public async Task<bool> IsHeldAsync(
        Guid eventId,
        Guid ticketTierId,
        Guid? seatId,
        CancellationToken cancellationToken = default)
    {
        var key = BuildKey(eventId, ticketTierId, seatId);
        return await _database.KeyExistsAsync(key);
    }

    private static string BuildKey(Guid eventId, Guid ticketTierId, Guid? seatId)
    {
        return seatId.HasValue
            ? $"hold:event:{eventId}:seat:{seatId.Value}"
            : $"hold:event:{eventId}:tier:{ticketTierId}";
    }
}
