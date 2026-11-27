using TR.API.Search.Application.Common.Messaging;
using TR.API.Search.Application.Features.Events.Models;

namespace TR.API.Search.Application.Features.Events.AutocompleteEvents;

public sealed record AutocompleteEventsQuery(
    string? Prefix,
    int Limit = 10
) : IQuery<IReadOnlyCollection<AutocompleteSuggestionDto>>;
