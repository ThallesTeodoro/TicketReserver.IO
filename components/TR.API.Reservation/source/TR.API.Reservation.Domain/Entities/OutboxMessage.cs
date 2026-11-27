using TR.API.Reservation.Domain.Enums;
using TR.API.Reservation.Domain.Exceptions;

namespace TR.API.Reservation.Domain.Entities;

public class OutboxMessage
{
    public Guid Id { get; private set; }
    public Guid CorrelationId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string AggregateType { get; private set; } = string.Empty;
    public Guid AggregateId { get; private set; }
    public string Payload { get; private set; } = string.Empty;
    public int DelaySeconds { get; private set; }
    public DateTime DeliverAfter { get; private set; }
    public OutboxStatus Status { get; private set; }
    public int RetryCount { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // Construtor protegido para o EF Core
    protected OutboxMessage() { }

    private OutboxMessage(
        Guid id,
        Guid correlationId,
        string eventType,
        string aggregateType,
        Guid aggregateId,
        string payload,
        int delaySeconds)
    {
        if (string.IsNullOrWhiteSpace(eventType))
            throw new DomainException("EventType não pode ser vazio.");

        if (string.IsNullOrWhiteSpace(aggregateType))
            throw new DomainException("AggregateType não pode ser vazio.");

        if (aggregateId == Guid.Empty)
            throw new DomainException("AggregateId não pode ser vazio.");

        if (string.IsNullOrWhiteSpace(payload))
            throw new DomainException("Payload não pode ser vazio.");

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        CorrelationId = correlationId == Guid.Empty ? Guid.NewGuid() : correlationId;
        EventType = eventType.Trim();
        AggregateType = aggregateType.Trim();
        AggregateId = aggregateId;
        Payload = payload;
        DelaySeconds = Math.Max(0, delaySeconds);
        CreatedAt = DateTime.UtcNow;
        DeliverAfter = CreatedAt.AddSeconds(DelaySeconds);
        Status = OutboxStatus.PENDING;
        RetryCount = 0;
    }

    public static OutboxMessage Create(
        Guid correlationId,
        string eventType,
        string aggregateType,
        Guid aggregateId,
        string payload,
        int delaySeconds = 0)
    {
        return new OutboxMessage(
            Guid.NewGuid(),
            correlationId,
            eventType,
            aggregateType,
            aggregateId,
            payload,
            delaySeconds);
    }

    public void MarkAsPublished(DateTime processedAt)
    {
        Status = OutboxStatus.PUBLISHED;
        ProcessedAt = processedAt;
        ErrorMessage = null;
    }

    public void MarkAsFailed(string error, DateTime processedAt)
    {
        Status = OutboxStatus.FAILED;
        ErrorMessage = error;
        ProcessedAt = processedAt;
    }

    public void IncrementRetry(string error, int nextDelaySeconds = 5)
    {
        RetryCount++;
        ErrorMessage = error;
        Status = OutboxStatus.PENDING;
        DeliverAfter = DateTime.UtcNow.AddSeconds(nextDelaySeconds);
    }
}
