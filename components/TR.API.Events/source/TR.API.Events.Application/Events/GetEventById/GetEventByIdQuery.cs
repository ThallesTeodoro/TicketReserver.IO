namespace TR.API.Events.Application.Events.GetEventById;

using TR.API.Events.Application.Common.Messaging;
using TR.API.Events.Domain.Contracts.Caching;
using TR.API.Events.Domain.Contracts.Persistence;
using TR.API.Events.Domain.Exceptions;

public sealed record GetEventByIdQuery(Guid Id) : IQuery<EventDetailsReadModel>;

public sealed class GetEventByIdQueryHandler(
    ICatalogReadRepository readRepository,
    ICacheService cacheService) : IQueryHandler<GetEventByIdQuery, EventDetailsReadModel>
{
    public async Task<EventDetailsReadModel> HandleAsync(GetEventByIdQuery query, CancellationToken cancellationToken = default)
    {
        var cacheKey = CacheKeys.EventById(query.Id);
        var cached = await cacheService.GetAsync<EventDetailsReadModel>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return cached;
        }

        var result = await readRepository.GetEventByIdAsync(query.Id, cancellationToken)
            ?? throw new NotFoundException($"Event with ID '{query.Id}' was not found.");

        await cacheService.SetAsync(cacheKey, result, CacheKeys.DefaultCatalogTtl, cancellationToken);

        return result;
    }
}
