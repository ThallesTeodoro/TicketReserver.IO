using TR.API.Search.Domain.ValueObjects;

namespace TR.API.Search.Domain.Entities;

public sealed class EventSearchDocument
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int AgeRestriction { get; init; }
    public string? BannerUrl { get; init; }
    public DateTimeOffset SalesStartAt { get; init; }
    public DateTimeOffset SalesEndAt { get; init; }
    public DateTimeOffset EventStartAt { get; init; }
    public DateTimeOffset EventEndAt { get; init; }
    public Guid VenueId { get; init; }
    public string VenueName { get; init; } = string.Empty;
    public string VenueCity { get; init; } = string.Empty;
    public string VenueState { get; init; } = string.Empty;
    public string VenueCountry { get; init; } = string.Empty;
    public GeoLocation VenueLocation { get; init; } = new(0, 0);
    public decimal MinPrice { get; init; }
    public decimal MaxPrice { get; init; }
    public IReadOnlyCollection<string> Tags { get; init; } = Array.Empty<string>();
    public IReadOnlyCollection<TicketTierSummary> Tiers { get; init; } = Array.Empty<TicketTierSummary>();
    public double? RelevanceScore { get; init; }
    public double? DistanceKm { get; init; }
}

public sealed class TicketTierSummary
{
    public Guid TierId { get; init; }
    public string TierName { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public string Status { get; init; } = string.Empty;
}

public sealed record SearchFacetEntry(string Key, long Count);

public sealed record SearchFacets(
    IReadOnlyCollection<SearchFacetEntry> Categories,
    IReadOnlyCollection<SearchFacetEntry> Cities);
