namespace TR.HW.ResTimeout.Persistence;

public interface IReservationTimeoutRepository
{
    Task<bool> IsMessageProcessedAsync(string messageId, string consumerGroup, CancellationToken ct = default);
    Task<ReservationTimeoutData?> GetReservationAsync(Guid reservationId, CancellationToken ct = default);
    Task<bool> ExpireReservationAsync(Guid reservationId, string messageId, string consumerGroup, CancellationToken ct = default);
    Task RecordProcessedMessageAsync(string messageId, string consumerGroup, string eventType, CancellationToken ct = default);
}
