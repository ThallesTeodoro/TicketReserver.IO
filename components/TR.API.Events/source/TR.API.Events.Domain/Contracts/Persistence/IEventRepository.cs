namespace TR.API.Events.Domain.Contracts.Persistence;

using TR.API.Events.Domain.Entities;

public interface IEventRepository
{
    Task<Event?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsBySlugAsync(string slug, Guid? excludeEventId = null, CancellationToken cancellationToken = default);
    Task<bool> VenueExistsAsync(Guid venueId, CancellationToken cancellationToken = default);
    Task AddAsync(Event evt, CancellationToken cancellationToken = default);
    void Update(Event evt);
}
