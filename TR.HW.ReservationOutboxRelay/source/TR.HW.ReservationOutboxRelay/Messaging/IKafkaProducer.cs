namespace TR.HW.ReservationOutboxRelay.Messaging;

using TR.HW.ReservationOutboxRelay.Persistence;

public interface IKafkaProducer : IDisposable
{
    Task ProduceAsync(OutboxMessageRecord message, CancellationToken cancellationToken = default);
    Task ProduceToDeadLetterQueueAsync(OutboxMessageRecord message, string reason, CancellationToken cancellationToken = default);
    string ResolveTopic(string eventType);
}
