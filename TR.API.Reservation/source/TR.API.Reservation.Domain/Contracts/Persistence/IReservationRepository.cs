namespace TR.API.Reservation.Domain.Contracts.Persistence;

using TR.API.Reservation.Domain.Entities;

public interface IReservationRepository
{
    Task AddAsync(Reservation reservation, CancellationToken cancellationToken = default);
    Task<Reservation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddOutboxMessageAsync(OutboxMessage outboxMessage, CancellationToken cancellationToken = default);
}

public interface IUnitOfWork
{
    Task<int> CommitAsync(CancellationToken cancellationToken = default);
}
