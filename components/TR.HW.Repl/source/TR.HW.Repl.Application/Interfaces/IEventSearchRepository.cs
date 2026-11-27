using TR.HW.Repl.Domain.Entities;

namespace TR.HW.Repl.Application.Interfaces;

public interface IEventSearchRepository
{
    Task UpsertEventAsync(EventSearchDocument document, CancellationToken cancellationToken = default);
    Task UpdateEventStatusAsync(Guid eventId, string status, DateTimeOffset updatedAt, CancellationToken cancellationToken = default);
    Task DeleteEventAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task<EventSearchDocument?> GetEventByIdAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task UpdateTierPricingAsync(Guid eventId, TicketTierSummary tier, CancellationToken cancellationToken = default);
    Task RemoveTierPricingAsync(Guid eventId, Guid tierId, CancellationToken cancellationToken = default);
    Task UpdateVenueInfoAsync(Guid venueId, string name, string city, string state, string country, double lat, double lon, CancellationToken cancellationToken = default);
}
