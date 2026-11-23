using TR.HW.Notification.Domain.Entities;

namespace TR.HW.Notification.Application.Interfaces;

public record NotificationDispatchResult(bool IsSuccess, string? ProviderReference, string? ErrorMessage);

public interface INotificationDispatcher
{
    Task<NotificationDispatchResult> DispatchAsync(CustomerNotification notification, CancellationToken cancellationToken = default);
}
