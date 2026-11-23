using TR.API.Search.Application.Features.Events.Models;
using TR.API.Search.Domain.Entities;

namespace TR.API.Search.Application.Features.Events.Mappings;

public static class EventSearchDtoMappingExtensions
{
    public static EventSearchItemDto ToDto(this EventSearchDocument doc)
    {
        return new EventSearchItemDto(
            doc.Id,
            doc.Title,
            doc.Slug,
            doc.Description,
            doc.Category,
            doc.Status,
            doc.AgeRestriction,
            doc.BannerUrl,
            doc.SalesStartAt,
            doc.SalesEndAt,
            doc.EventStartAt,
            doc.EventEndAt,
            doc.VenueId,
            doc.VenueName,
            doc.VenueCity,
            doc.VenueState,
            doc.VenueCountry,
            doc.VenueLocation.Latitude,
            doc.VenueLocation.Longitude,
            doc.MinPrice,
            doc.MaxPrice,
            doc.Tags,
            doc.Tiers.Select(t => t.ToDto()).ToList(),
            doc.RelevanceScore,
            doc.DistanceKm);
    }

    public static TicketTierSummaryDto ToDto(this TicketTierSummary tier)
    {
        return new TicketTierSummaryDto(
            tier.TierId,
            tier.TierName,
            tier.Price,
            tier.Status);
    }

    public static SearchFacetsDto ToDto(this SearchFacets facets)
    {
        var categories = facets.Categories
            .Select(c => new FacetCountDto(c.Key, c.Count))
            .ToList();

        var cities = facets.Cities
            .Select(c => new FacetCountDto(c.Key, c.Count))
            .ToList();

        return new SearchFacetsDto(categories, cities);
    }
}
