using TR.HW.Notification.Domain.Enums;
using TR.HW.Notification.Domain.Exceptions;

namespace TR.HW.Notification.Domain.Entities;

public sealed class CustomerNotification
{
    public Guid Id { get; private set; }
    public Guid ReservationId { get; private set; }
    public Guid CustomerId { get; private set; }
    public string Recipient { get; private set; } = string.Empty;
    public NotificationType Type { get; private set; }
    public NotificationChannel Channel { get; private set; }
    public NotificationStatus Status { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public string? MetadataJson { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }
    public DateTimeOffset? FailedAt { get; private set; }
    public string? FailureReason { get; private set; }
    public string? ExternalProviderReference { get; private set; }

    private CustomerNotification()
    {
    }

    public static CustomerNotification Create(
        Guid id,
        Guid reservationId,
        Guid customerId,
        string recipient,
        NotificationType type,
        NotificationChannel channel,
        string title,
        string content,
        string? metadataJson = null,
        DateTimeOffset? createdAt = null)
    {
        if (id == Guid.Empty)
            throw new DomainException("Notification identifier cannot be empty.");

        if (reservationId == Guid.Empty)
            throw new DomainException("Reservation identifier cannot be empty.");

        if (customerId == Guid.Empty)
            throw new DomainException("Customer identifier cannot be empty.");

        if (string.IsNullOrWhiteSpace(recipient))
            throw new DomainException("Recipient address or token cannot be empty.");

        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("Notification title cannot be empty.");

        if (string.IsNullOrWhiteSpace(content))
            throw new DomainException("Notification content cannot be empty.");

        return new CustomerNotification
        {
            Id = id,
            ReservationId = reservationId,
            CustomerId = customerId,
            Recipient = recipient.Trim(),
            Type = type,
            Channel = channel,
            Status = NotificationStatus.Pending,
            Title = title.Trim(),
            Content = content.Trim(),
            MetadataJson = string.IsNullOrWhiteSpace(metadataJson) ? null : metadataJson.Trim(),
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow
        };
    }

    public void MarkAsSent(string providerReference, DateTimeOffset sentAt)
    {
        if (string.IsNullOrWhiteSpace(providerReference))
            throw new DomainException("Provider delivery reference cannot be empty.");

        if (Status == NotificationStatus.Sent)
            return; // Idempotent

        Status = NotificationStatus.Sent;
        ExternalProviderReference = providerReference.Trim();
        SentAt = sentAt;
        FailureReason = null;
        FailedAt = null;
    }

    public void MarkAsFailed(string failureReason, DateTimeOffset failedAt)
    {
        if (string.IsNullOrWhiteSpace(failureReason))
            throw new DomainException("Failure reason cannot be empty.");

        if (Status == NotificationStatus.Sent)
            throw new DomainException("Cannot mark an already sent notification as failed.");

        Status = NotificationStatus.Failed;
        FailureReason = failureReason.Trim();
        FailedAt = failedAt;
    }
}
