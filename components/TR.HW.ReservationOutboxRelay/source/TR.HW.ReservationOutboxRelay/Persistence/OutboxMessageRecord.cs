namespace TR.HW.ReservationOutboxRelay.Persistence;

public record OutboxMessageRecord(
    Guid Id,
    Guid CorrelationId,
    string EventType,
    string AggregateType,
    Guid AggregateId,
    string Payload,
    int DelaySeconds,
    DateTime DeliverAfter,
    int Status,
    int RetryCount,
    string? ErrorMessage,
    DateTime? ProcessedAt,
    DateTime CreatedAt);
