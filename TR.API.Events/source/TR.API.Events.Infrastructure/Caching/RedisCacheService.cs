namespace TR.API.Events.Infrastructure.Caching;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using TR.API.Events.Domain.Contracts.Caching;

public sealed class RedisCacheService(
    IConnectionMultiplexer connectionMultiplexer,
    ILogger<RedisCacheService> logger) : ICacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = connectionMultiplexer.GetDatabase();
            var value = await db.StringGetAsync(key);
            if (!value.HasValue)
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>((string)value!, JsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis cache GET failed for key '{Key}'. Falling back to data source.", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = connectionMultiplexer.GetDatabase();
            var json = JsonSerializer.Serialize(value, JsonOptions);
            await db.StringSetAsync(key, json, ttl);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis cache SET failed for key '{Key}'.", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var db = connectionMultiplexer.GetDatabase();
            await db.KeyDeleteAsync(key);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Redis cache REMOVE failed for key '{Key}'.", key);
        }
    }
}
