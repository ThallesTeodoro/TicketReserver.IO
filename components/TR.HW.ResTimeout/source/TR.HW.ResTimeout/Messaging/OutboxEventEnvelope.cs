namespace TR.HW.ResTimeout.Messaging;

using System.Text.Json;

public sealed record OutboxEventEnvelope(
    Guid Id,
    Guid CorrelationId,
    string EventType,
    Guid AggregateId,
    DateTime CreatedAt,
    JsonElement Payload);
