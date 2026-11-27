namespace TR.API.Reservation.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Npgsql.NameTranslation;
using TR.API.Reservation.Domain.Contracts.Persistence;
using TR.API.Reservation.Domain.Entities;
using TR.API.Reservation.Domain.Enums;

public class ReservationDbContext(DbContextOptions<ReservationDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<ReservationItem> ReservationItems => Set<ReservationItem>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    public Task<int> CommitAsync(CancellationToken cancellationToken = default)
    {
        return SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var translator = new NpgsqlNullNameTranslator();
        modelBuilder.HasPostgresEnum<ReservationStatus>(name: "reservation_status_enum", nameTranslator: translator);
        modelBuilder.HasPostgresEnum<OutboxStatus>(name: "outbox_status_enum", nameTranslator: translator);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ReservationDbContext).Assembly);
    }
}
