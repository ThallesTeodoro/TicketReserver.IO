using TR.API.Search.Application.Features.Events.Models;
using TR.API.Search.Application.Features.Events.SearchEvents;
using TR.API.Search.Domain.Entities;

namespace TR.API.Search.Application.Interfaces;

public interface IEventSearchEngine
{
    Task<EventSearchResultDto> SearchEventsAsync(SearchEventsQuery query, CancellationToken ct = default);
    Task<IReadOnlyCollection<AutocompleteSuggestionDto>> AutocompleteEventsAsync(string? prefix, int limit, CancellationToken ct = default);
    Task<EventSearchDocument?> GetEventByIdAsync(Guid eventId, CancellationToken ct = default);
}
