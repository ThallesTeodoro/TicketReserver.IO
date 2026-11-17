namespace TR.API.Events.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Npgsql.NameTranslation;
using TR.API.Events.Domain.Contracts.Persistence;
using TR.API.Events.Domain.Entities;
using TR.API.Events.Domain.Enums;

public class EventsDbContext(DbContextOptions<EventsDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<SeatSection> SeatSections => Set<SeatSection>();
    public DbSet<TicketTier> TicketTiers => Set<TicketTier>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var translator = new NpgsqlNullNameTranslator();
        modelBuilder.HasPostgresEnum<EventStatus>(name: "event_status_enum", nameTranslator: translator);
        modelBuilder.HasPostgresEnum<TicketTierType>(name: "ticket_tier_type_enum", nameTranslator: translator);
        modelBuilder.HasPostgresEnum<TicketTierStatus>(name: "ticket_tier_status_enum", nameTranslator: translator);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EventsDbContext).Assembly);
    }
}
