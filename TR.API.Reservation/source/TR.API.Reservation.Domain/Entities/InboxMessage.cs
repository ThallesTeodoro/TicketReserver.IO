using TR.API.Reservation.Domain.Exceptions;

namespace TR.API.Reservation.Domain.Entities;

public class InboxMessage
{
    public string MessageId { get; private set; } = string.Empty;
    public string ConsumerGroup { get; private set; } = string.Empty;
    public string EventType { get; private set; } = string.Empty;
    public DateTime ProcessedAt { get; private set; }

    // Construtor protegido para o EF Core
    protected InboxMessage() { }

    private InboxMessage(string messageId, string consumerGroup, string eventType)
    {
        if (string.IsNullOrWhiteSpace(messageId))
            throw new DomainException("MessageId não pode ser vazio.");

        if (string.IsNullOrWhiteSpace(consumerGroup))
            throw new DomainException("ConsumerGroup não pode ser vazio.");

        if (string.IsNullOrWhiteSpace(eventType))
            throw new DomainException("EventType não pode ser vazio.");

        MessageId = messageId.Trim();
        ConsumerGroup = consumerGroup.Trim();
        EventType = eventType.Trim();
        ProcessedAt = DateTime.UtcNow;
    }

    public static InboxMessage Create(string messageId, string consumerGroup, string eventType)
    {
        return new InboxMessage(messageId, consumerGroup, eventType);
    }
}
