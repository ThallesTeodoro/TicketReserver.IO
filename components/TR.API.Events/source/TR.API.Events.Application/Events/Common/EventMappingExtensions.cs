namespace TR.API.Events.Application.Events.Common;

using TR.API.Events.Domain.Entities;

public sealed record TicketTierResponse(
    Guid Id,
    Guid SectionId,
    string Name,
    string TierType,
    decimal Price,
    string Currency,
    int TotalQuantity,
    DateTimeOffset SalesStartAt,
    DateTimeOffset SalesEndAt,
    string Status);

public sealed record EventResponse(
    Guid Id,
    Guid VenueId,
    string Title,
    string Slug,
    string? Description,
    string Category,
    string Status,
    DateTimeOffset SalesStartAt,
    DateTimeOffset SalesEndAt,
    DateTimeOffset EventStartAt,
    DateTimeOffset EventEndAt,
    int AgeRestriction,
    string? BannerUrl,
    int MaxTicketsPerUser,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyCollection<TicketTierResponse> TicketTiers);

public static class EventMappingExtensions
{
    public static EventResponse ToResponse(this Event evt)
    {
        return new EventResponse(
            evt.Id,
            evt.VenueId,
            evt.Title,
            evt.Slug,
            evt.Description,
            evt.Category,
            evt.Status.ToString(),
            evt.SalesStartAt,
            evt.SalesEndAt,
            evt.EventStartAt,
            evt.EventEndAt,
            evt.AgeRestriction,
            evt.BannerUrl,
            evt.MaxTicketsPerUser,
            evt.Version,
            evt.CreatedAt,
            evt.UpdatedAt,
            evt.TicketTiers.Select(t => t.ToResponse()).ToList().AsReadOnly());
    }

    public static TicketTierResponse ToResponse(this TicketTier tier)
    {
        return new TicketTierResponse(
            tier.Id,
            tier.SectionId,
            tier.Name,
            tier.TierType.ToString(),
            tier.Price,
            tier.Currency,
            tier.TotalQuantity,
            tier.SalesStartAt,
            tier.SalesEndAt,
            tier.Status.ToString());
    }
}
