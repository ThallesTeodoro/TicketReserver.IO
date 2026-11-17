namespace TR.API.Events.Application.Venues.ListVenues;

using TR.API.Events.Application.Common.Messaging;
using TR.API.Events.Domain.Contracts.Persistence;

public sealed record ListVenuesQuery : IQuery<IReadOnlyCollection<VenueReadModel>>;

public sealed class ListVenuesQueryHandler(ICatalogReadRepository readRepository)
    : IQueryHandler<ListVenuesQuery, IReadOnlyCollection<VenueReadModel>>
{
    public async Task<IReadOnlyCollection<VenueReadModel>> HandleAsync(ListVenuesQuery query, CancellationToken cancellationToken = default)
    {
        return await readRepository.ListVenuesAsync(cancellationToken);
    }
}
