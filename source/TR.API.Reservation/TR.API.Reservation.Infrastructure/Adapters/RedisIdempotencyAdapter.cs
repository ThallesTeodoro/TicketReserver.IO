using StackExchange.Redis;
using TR.API.Reservation.Application.Ports;
using TR.API.Reservation.Domain.Constants;

namespace TR.API.Reservation.Infrastructure.Adapters;

/// <summary>
/// Adapter de infraestrutura para idempotência utilizando Redis.
/// </summary>
public class RedisIdempotencyAdapter : IIdempotencyPort
{
    private readonly IDatabase _database;

    public RedisIdempotencyAdapter(IConnectionMultiplexer connectionMultiplexer)
    {
        _database = connectionMultiplexer.GetDatabase();
    }

    public async Task<bool> IsIdempotentAsync(string key, TimeSpan ttl)
    {
        var redisKey = $"{RedisKeys.IdempotencyPrefix}{key}";
        
        // SET NX: Define a chave apenas se ela não existir
        return await _database.StringSetAsync(redisKey, "locked", ttl, When.NotExists);
    }
}
