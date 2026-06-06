namespace TR.HW.OutboxRelay.Domain.Entities;

/// <summary>
/// Representa uma mensagem pendente na tabela de Outbox para ser enviada ao Broker.
/// </summary>
public record OutboxMessage(
    Guid Id,
    string CorrelationId,
    string EventType,
    string Payload,
    DateTime CreatedAt);
