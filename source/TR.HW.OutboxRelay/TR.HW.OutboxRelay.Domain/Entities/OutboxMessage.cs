using TR.HW.OutboxRelay.Domain.Enums;

namespace TR.HW.OutboxRelay.Domain.Entities;

/// <summary>
/// Representa uma mensagem na tabela de Outbox com controle de estado e retries.
/// </summary>
public record OutboxMessage(
    Guid Id,
    string CorrelationId,
    string EventName,
    string EventType,
    string Payload,
    OutboxStatus Status,
    int Attempts,
    DateTime CreatedAt,
    DateTime? LastAttemptAt = null,
    string? ErrorReason = null,
    DateTime? ProcessedAt = null);
