namespace TR.API.Events.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using TR.API.Events.Domain.Contracts.Persistence;
using TR.API.Events.Domain.Entities;

public sealed class EventRepository(EventsDbContext dbContext) : IEventRepository
{
    public async Task<Event?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Events
            .Include(e => e.TicketTiers)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<bool> ExistsBySlugAsync(string slug, Guid? excludeEventId = null, CancellationToken cancellationToken = default)
    {
        var normalizedSlug = slug.Trim().ToLowerInvariant();
        var query = dbContext.Events.AsNoTracking().Where(e => e.Slug == normalizedSlug);

        if (excludeEventId.HasValue)
        {
            query = query.Where(e => e.Id != excludeEventId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }

    public async Task<bool> VenueExistsAsync(Guid venueId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Venues.AsNoTracking().AnyAsync(v => v.Id == venueId, cancellationToken);
    }

    public async Task AddAsync(Event evt, CancellationToken cancellationToken = default)
    {
        await dbContext.Events.AddAsync(evt, cancellationToken);
    }

    public void Update(Event evt)
    {
        dbContext.Events.Update(evt);
    }
}
