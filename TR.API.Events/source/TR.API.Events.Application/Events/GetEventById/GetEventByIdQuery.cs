namespace TR.API.Events.Application.Events.GetEventById;

using TR.API.Events.Application.Common.Messaging;
using TR.API.Events.Domain.Contracts.Persistence;
using TR.API.Events.Domain.Exceptions;

public sealed record GetEventByIdQuery(Guid Id) : IQuery<EventDetailsReadModel>;

public sealed class GetEventByIdQueryHandler(ICatalogReadRepository readRepository)
    : IQueryHandler<GetEventByIdQuery, EventDetailsReadModel>
{
    public async Task<EventDetailsReadModel> HandleAsync(GetEventByIdQuery query, CancellationToken cancellationToken = default)
    {
        var result = await readRepository.GetEventByIdAsync(query.Id, cancellationToken)
            ?? throw new NotFoundException($"Event with ID '{query.Id}' was not found.");

        return result;
    }
}
