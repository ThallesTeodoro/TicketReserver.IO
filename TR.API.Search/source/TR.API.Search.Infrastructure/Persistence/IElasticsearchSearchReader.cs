using TR.API.Search.Application.Features.Events.SearchEvents;
using TR.API.Search.Infrastructure.Models;

namespace TR.API.Search.Infrastructure.Persistence;

public interface IElasticsearchSearchReader
{
    Task<EsSearchResult<EsEventDocument>> SearchAsync(SearchEventsQuery query, CancellationToken ct = default);
    Task<IReadOnlyCollection<EsAutocompleteItem>> AutocompleteAsync(string prefix, int limit, CancellationToken ct = default);
    Task<EsEventDocument?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> PingAsync(CancellationToken ct = default);
}
