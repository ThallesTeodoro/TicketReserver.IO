namespace TR.API.Search.Application.Features.Events.Models;

public sealed record EventSearchResultDto(
    IReadOnlyCollection<EventSearchItemDto> Items,
    long TotalCount,
    int Page,
    int PageSize,
    SearchFacetsDto Facets);

public sealed record EventSearchItemDto(
    Guid Id,
    string Title,
    string Slug,
    string Description,
    string Category,
    string Status,
    int AgeRestriction,
    string? BannerUrl,
    DateTimeOffset SalesStartAt,
    DateTimeOffset SalesEndAt,
    DateTimeOffset EventStartAt,
    DateTimeOffset EventEndAt,
    Guid VenueId,
    string VenueName,
    string VenueCity,
    string VenueState,
    string VenueCountry,
    double VenueLatitude,
    double VenueLongitude,
    decimal MinPrice,
    decimal MaxPrice,
    IReadOnlyCollection<string> Tags,
    IReadOnlyCollection<TicketTierSummaryDto> Tiers,
    double? RelevanceScore = null,
    double? DistanceKm = null);

public sealed record TicketTierSummaryDto(
    Guid TierId,
    string TierName,
    decimal Price,
    string Status);

public sealed record FacetCountDto(string Key, long Count);

public sealed record SearchFacetsDto(
    IReadOnlyCollection<FacetCountDto> Categories,
    IReadOnlyCollection<FacetCountDto> Cities);

public sealed record AutocompleteSuggestionDto(
    Guid Id,
    string Title,
    string Category);
