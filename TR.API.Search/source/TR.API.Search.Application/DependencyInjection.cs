using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using TR.API.Search.Application.Common.Messaging;
using TR.API.Search.Application.Features.Events.AutocompleteEvents;
using TR.API.Search.Application.Features.Events.GetEventDetails;
using TR.API.Search.Application.Features.Events.Models;
using TR.API.Search.Application.Features.Events.QueryHandlers;
using TR.API.Search.Application.Features.Events.SearchEvents;
using TR.API.Search.Application.Features.Events.Validators;

namespace TR.API.Search.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IValidator<SearchEventsQuery>, SearchEventsQueryValidator>();
        services.AddScoped<IValidator<AutocompleteEventsQuery>, AutocompleteEventsQueryValidator>();
        services.AddScoped<IValidator<GetEventByIdQuery>, GetEventByIdQueryValidator>();

        services.AddScoped<IQueryHandler<SearchEventsQuery, EventSearchResultDto>, SearchEventsQueryHandler>();
        services.AddScoped<IQueryHandler<AutocompleteEventsQuery, IReadOnlyCollection<AutocompleteSuggestionDto>>, AutocompleteEventsQueryHandler>();
        services.AddScoped<IQueryHandler<GetEventByIdQuery, EventSearchItemDto?>, GetEventByIdQueryHandler>();

        return services;
    }
}
