namespace TR.API.Reservation.Infrastructure.Persistence.Repositories;

using Microsoft.EntityFrameworkCore;
using TR.API.Reservation.Domain.Contracts.Persistence;
using TR.API.Reservation.Domain.Entities;
using TR.API.Reservation.Infrastructure.Persistence;

public sealed class ReservationRepository(ReservationDbContext dbContext) : IReservationRepository
{
    public async Task AddAsync(Reservation reservation, CancellationToken cancellationToken = default)
    {
        await dbContext.Reservations.AddAsync(reservation, cancellationToken);
    }

    public async Task<Reservation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Reservations
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task AddOutboxMessageAsync(OutboxMessage outboxMessage, CancellationToken cancellationToken = default)
    {
        await dbContext.OutboxMessages.AddAsync(outboxMessage, cancellationToken);
    }
}
