using FluentValidation;
using TR.API.Search.Application.Common.Messaging;
using TR.API.Search.Application.Features.Events.AutocompleteEvents;
using TR.API.Search.Application.Features.Events.GetEventDetails;
using TR.API.Search.Application.Features.Events.Mappings;
using TR.API.Search.Application.Features.Events.Models;
using TR.API.Search.Application.Features.Events.SearchEvents;
using TR.API.Search.Application.Interfaces;

namespace TR.API.Search.Application.Features.Events.QueryHandlers;

public class SearchEventsQueryHandler : IQueryHandler<SearchEventsQuery, EventSearchResultDto>
{
    private readonly IEventSearchEngine _searchEngine;
    private readonly IValidator<SearchEventsQuery> _validator;

    public SearchEventsQueryHandler(IEventSearchEngine searchEngine, IValidator<SearchEventsQuery> validator)
    {
        _searchEngine = searchEngine ?? throw new ArgumentNullException(nameof(searchEngine));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    public async Task<EventSearchResultDto> HandleAsync(SearchEventsQuery query, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(query, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        return await _searchEngine.SearchEventsAsync(query, cancellationToken);
    }
}

public class AutocompleteEventsQueryHandler : IQueryHandler<AutocompleteEventsQuery, IReadOnlyCollection<AutocompleteSuggestionDto>>
{
    private readonly IEventSearchEngine _searchEngine;
    private readonly IValidator<AutocompleteEventsQuery> _validator;

    public AutocompleteEventsQueryHandler(IEventSearchEngine searchEngine, IValidator<AutocompleteEventsQuery> validator)
    {
        _searchEngine = searchEngine ?? throw new ArgumentNullException(nameof(searchEngine));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    public async Task<IReadOnlyCollection<AutocompleteSuggestionDto>> HandleAsync(AutocompleteEventsQuery query, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(query, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        if (string.IsNullOrWhiteSpace(query.Prefix))
        {
            return Array.Empty<AutocompleteSuggestionDto>();
        }

        return await _searchEngine.AutocompleteEventsAsync(query.Prefix.Trim(), query.Limit, cancellationToken);
    }
}

public class GetEventByIdQueryHandler : IQueryHandler<GetEventByIdQuery, EventSearchItemDto?>
{
    private readonly IEventSearchEngine _searchEngine;
    private readonly IValidator<GetEventByIdQuery> _validator;

    public GetEventByIdQueryHandler(IEventSearchEngine searchEngine, IValidator<GetEventByIdQuery> validator)
    {
        _searchEngine = searchEngine ?? throw new ArgumentNullException(nameof(searchEngine));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    public async Task<EventSearchItemDto?> HandleAsync(GetEventByIdQuery query, CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(query, cancellationToken);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var doc = await _searchEngine.GetEventByIdAsync(query.EventId, cancellationToken);
        return doc?.ToDto();
    }
}
