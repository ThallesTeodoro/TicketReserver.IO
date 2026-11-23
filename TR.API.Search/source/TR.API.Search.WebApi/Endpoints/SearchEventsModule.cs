using Carter;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using TR.API.Search.Application.Common.Messaging;
using TR.API.Search.Application.Features.Events.AutocompleteEvents;
using TR.API.Search.Application.Features.Events.GetEventDetails;
using TR.API.Search.Application.Features.Events.Models;
using TR.API.Search.Application.Features.Events.SearchEvents;
using TR.API.Search.Domain.ValueObjects;
using TR.API.Search.WebApi.Contracts;

namespace TR.API.Search.WebApi.Endpoints;

public class SearchEventsModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/search/events")
            .WithTags("Events Search");

        group.MapGet("/", async (
            string? q,
            string? category,
            string? status,
            DateTimeOffset? from_date,
            DateTimeOffset? to_date,
            decimal? min_price,
            decimal? max_price,
            double? lat,
            double? lon,
            double? radius_km,
            string? sort_by,
            int? page,
            int? page_size,
            IQueryHandler<SearchEventsQuery, EventSearchResultDto> handler,
            CancellationToken ct) =>
        {
            var sortOption = SearchSortOption.Relevance;
            if (!string.IsNullOrWhiteSpace(sort_by) && Enum.TryParse<SearchSortOption>(sort_by, true, out var parsedSort))
            {
                sortOption = parsedSort;
            }

            var query = new SearchEventsQuery(
                Query: q,
                Category: category,
                Status: string.IsNullOrWhiteSpace(status) ? "PUBLISHED" : status,
                FromDate: from_date,
                ToDate: to_date,
                MinPrice: min_price,
                MaxPrice: max_price,
                Latitude: lat,
                Longitude: lon,
                RadiusKm: radius_km,
                SortBy: sortOption,
                Page: page ?? 1,
                PageSize: page_size ?? 20
            );

            var result = await handler.HandleAsync(query, ct);
            return Results.Ok(JsonResponse<EventSearchResultDto, object>.Success(result));
        })
        .WithName("SearchEvents");

        group.MapGet("/autocomplete", async (
            string? prefix,
            int? limit,
            IQueryHandler<AutocompleteEventsQuery, IReadOnlyCollection<AutocompleteSuggestionDto>> handler,
            CancellationToken ct) =>
        {
            var query = new AutocompleteEventsQuery(prefix, limit ?? 10);
            var result = await handler.HandleAsync(query, ct);
            return Results.Ok(JsonResponse<IReadOnlyCollection<AutocompleteSuggestionDto>, object>.Success(result));
        })
        .WithName("AutocompleteEvents");

        group.MapGet("/{id:guid}", async (
            Guid id,
            IQueryHandler<GetEventByIdQuery, EventSearchItemDto?> handler,
            CancellationToken ct) =>
        {
            var query = new GetEventByIdQuery(id);
            var result = await handler.HandleAsync(query, ct);

            if (result == null)
            {
                return Results.NotFound(JsonResponse<object, object>.Failure(404, new[] { "Event not found" }, "Event not found"));
            }

            return Results.Ok(JsonResponse<EventSearchItemDto, object>.Success(result));
        })
        .WithName("GetEventById");
    }
}
