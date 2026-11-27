namespace TR.API.Events.Application.Events.UpdateEvent;

using TR.API.Events.Application.Common.Messaging;
using TR.API.Events.Application.Events.Common;

public sealed record UpdateEventCommand(
    Guid Id,
    Guid VenueId,
    string Title,
    string Slug,
    string? Description,
    string Category,
    DateTimeOffset SalesStartAt,
    DateTimeOffset SalesEndAt,
    DateTimeOffset EventStartAt,
    DateTimeOffset EventEndAt,
    int AgeRestriction,
    string? BannerUrl,
    int MaxTicketsPerUser) : ICommand<EventResponse>;
