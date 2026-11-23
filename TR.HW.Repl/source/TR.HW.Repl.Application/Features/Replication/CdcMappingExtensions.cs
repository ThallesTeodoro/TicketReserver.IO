using TR.HW.Repl.Domain.Entities;
using TR.HW.Repl.Domain.ValueObjects;

namespace TR.HW.Repl.Application.Features.Replication;

public static class CdcMappingExtensions
{
    public static EventSearchDocument ToEventSearchDocument(
        this ReplicateEventChangeCommand command,
        EventSearchDocument? existingDocument = null)
    {
        var schedule = new EventScheduleInfo(
            command.SalesStartAt ?? DateTimeOffset.UtcNow,
            command.SalesEndAt ?? DateTimeOffset.UtcNow.AddDays(30),
            command.EventStartAt ?? DateTimeOffset.UtcNow.AddDays(30),
            command.EventEndAt ?? DateTimeOffset.UtcNow.AddDays(30).AddHours(4));

        var venue = new VenueSearchInfo(
            command.VenueId ?? Guid.NewGuid(),
            command.VenueName ?? "Venue",
            command.VenueCity ?? "Cidade",
            command.VenueState ?? "UF",
            command.VenueCountry ?? "BR",
            new GeoLocation(command.VenueLatitude ?? 0, command.VenueLongitude ?? 0));

        if (existingDocument != null)
        {
            existingDocument.UpdateEventDetails(
                command.Title ?? existingDocument.Title,
                command.Slug ?? existingDocument.Slug,
                command.Description ?? existingDocument.Description,
                command.Category ?? existingDocument.Category,
                command.Status ?? existingDocument.Status,
                command.AgeRestriction ?? existingDocument.AgeRestriction,
                command.BannerUrl ?? existingDocument.BannerUrl,
                schedule,
                command.Tags ?? existingDocument.Tags);

            existingDocument.UpdateVenue(venue);
            return existingDocument;
        }

        return EventSearchDocument.Create(
            command.EventId,
            command.Title ?? "Event",
            command.Slug ?? "event-slug",
            command.Description ?? string.Empty,
            command.Category ?? "GENERAL",
            command.Status ?? "DRAFT",
            command.AgeRestriction ?? 0,
            command.BannerUrl,
            schedule,
            venue,
            command.Tags);
    }

    public static TicketTierSummary ToTicketTierSummary(this ReplicateTicketTierChangeCommand command)
    {
        return TicketTierSummary.Create(
            command.TierId,
            command.TierName ?? "Tier",
            command.Price ?? 0m,
            command.Status ?? "ACTIVE");
    }
}
