namespace TR.API.Events.Domain.Contracts.Caching;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}

public static class CacheKeys
{
    public static readonly TimeSpan DefaultCatalogTtl = TimeSpan.FromHours(1);

    public static string EventById(Guid id) => $"catalog:event:{id}";
    public static string VenuesList() => "catalog:venues";
}
