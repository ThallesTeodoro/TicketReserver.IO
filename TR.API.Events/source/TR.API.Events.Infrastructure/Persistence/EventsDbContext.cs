namespace TR.API.Events.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

public class EventsDbContext(DbContextOptions<EventsDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EventsDbContext).Assembly);
    }
}
