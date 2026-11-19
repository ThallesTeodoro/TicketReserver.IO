namespace TR.HW.ReservationOutboxRelay.Messaging;

public record OutboxEventEnvelope(
    Guid Id,
    Guid CorrelationId,
    string EventType,
    Guid AggregateId,
    DateTime Timestamp,
    string Payload);
