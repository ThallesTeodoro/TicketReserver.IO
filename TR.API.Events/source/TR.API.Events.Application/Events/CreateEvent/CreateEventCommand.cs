namespace TR.API.Events.Application.Events.CreateEvent;

using TR.API.Events.Application.Common.Messaging;
using TR.API.Events.Application.Events.Common;

public sealed record CreateTicketTierItem(
    Guid SectionId,
    string Name,
    string TierType,
    decimal Price,
    string Currency,
    int TotalQuantity,
    DateTimeOffset SalesStartAt,
    DateTimeOffset SalesEndAt);

public sealed record CreateEventCommand(
    Guid VenueId,
    string Title,
    string Slug,
    string? Description,
    string Category,
    DateTimeOffset SalesStartAt,
    DateTimeOffset SalesEndAt,
    DateTimeOffset EventStartAt,
    DateTimeOffset EventEndAt,
    int AgeRestriction = 0,
    string? BannerUrl = null,
    int MaxTicketsPerUser = 4,
    IReadOnlyCollection<CreateTicketTierItem>? TicketTiers = null) : ICommand<EventResponse>;
