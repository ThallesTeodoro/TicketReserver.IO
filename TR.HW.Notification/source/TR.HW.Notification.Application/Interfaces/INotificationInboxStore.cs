namespace TR.HW.Notification.Application.Interfaces;

public interface INotificationInboxStore
{
    Task<bool> HasBeenProcessedAsync(string consumerGroup, string messageId, CancellationToken cancellationToken = default);
    Task MarkAsProcessedAsync(string consumerGroup, string messageId, TimeSpan? retention = null, CancellationToken cancellationToken = default);
}
