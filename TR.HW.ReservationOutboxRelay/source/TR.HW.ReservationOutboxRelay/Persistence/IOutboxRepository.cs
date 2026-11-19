namespace TR.HW.ReservationOutboxRelay.Persistence;

public interface IOutboxRepository
{
    Task<IReadOnlyList<OutboxMessageRecord>> GetPendingBatchAsync(int batchSize, CancellationToken cancellationToken = default);
    Task MarkAsPublishedAsync(Guid id, DateTime processedAt, CancellationToken cancellationToken = default);
    Task MarkAsFailedAsync(Guid id, string errorMessage, DateTime processedAt, CancellationToken cancellationToken = default);
    Task IncrementRetryAsync(Guid id, string errorMessage, int nextDelaySeconds, CancellationToken cancellationToken = default);
}
