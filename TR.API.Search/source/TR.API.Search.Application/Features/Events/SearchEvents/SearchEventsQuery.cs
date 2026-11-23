using TR.API.Search.Application.Common.Messaging;
using TR.API.Search.Application.Features.Events.Models;
using TR.API.Search.Domain.ValueObjects;

namespace TR.API.Search.Application.Features.Events.SearchEvents;

public sealed record SearchEventsQuery(
    string? Query = null,
    string? Category = null,
    string? Status = "PUBLISHED",
    DateTimeOffset? FromDate = null,
    DateTimeOffset? ToDate = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    double? Latitude = null,
    double? Longitude = null,
    double? RadiusKm = null,
    SearchSortOption SortBy = SearchSortOption.Relevance,
    int Page = 1,
    int PageSize = 20
) : IQuery<EventSearchResultDto>;
