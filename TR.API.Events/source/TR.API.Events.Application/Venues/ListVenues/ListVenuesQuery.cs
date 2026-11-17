namespace TR.API.Events.Application.Venues.ListVenues;

using TR.API.Events.Application.Common.Messaging;
using TR.API.Events.Domain.Contracts.Caching;
using TR.API.Events.Domain.Contracts.Persistence;

public sealed record ListVenuesQuery : IQuery<IReadOnlyCollection<VenueReadModel>>;

public sealed class ListVenuesQueryHandler(
    ICatalogReadRepository readRepository,
    ICacheService cacheService) : IQueryHandler<ListVenuesQuery, IReadOnlyCollection<VenueReadModel>>
{
    public async Task<IReadOnlyCollection<VenueReadModel>> HandleAsync(ListVenuesQuery query, CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeys.VenuesList();
        var cached = await cacheService.GetAsync<List<VenueReadModel>>(cacheKey, cancellationToken);
        if (cached is { Count: > 0 })
        {
            return cached.AsReadOnly();
        }

        var venues = await readRepository.ListVenuesAsync(cancellationToken);
        if (venues.Count > 0)
        {
            await cacheService.SetAsync(cacheKey, venues, CacheKeys.DefaultCatalogTtl, cancellationToken);
        }

        return venues;
    }
}
