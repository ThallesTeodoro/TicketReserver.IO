namespace TR.API.Events.Application.Events.ListEvents;

using TR.API.Events.Application.Common.Messaging;
using TR.API.Events.Domain.Contracts.Persistence;

public sealed record PaginatedEventsResponse(
    IReadOnlyCollection<EventSummaryReadModel> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record ListEventsQuery(
    int Page = 1,
    int PageSize = 10,
    string? Category = null,
    string? Status = null,
    string? City = null) : IQuery<PaginatedEventsResponse>;

public sealed class ListEventsQueryHandler(ICatalogReadRepository readRepository)
    : IQueryHandler<ListEventsQuery, PaginatedEventsResponse>
{
    public async Task<PaginatedEventsResponse> HandleAsync(ListEventsQuery query, CancellationToken cancellationToken = default)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize switch
        {
            < 1 => 10,
            > 100 => 100,
            _ => query.PageSize
        };

        var (items, totalCount) = await readRepository.ListEventsAsync(
            page,
            pageSize,
            query.Category,
            query.Status,
            query.City,
            cancellationToken);

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PaginatedEventsResponse(items, page, pageSize, totalCount, totalPages);
    }
}
