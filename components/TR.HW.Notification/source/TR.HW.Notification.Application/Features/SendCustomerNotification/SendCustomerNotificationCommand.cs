using TR.HW.Notification.Application.Common.Messaging;
using TR.HW.Notification.Domain.Enums;

namespace TR.HW.Notification.Application.Features.SendCustomerNotification;

public record SendCustomerNotificationCommand(
    Guid ReservationId,
    Guid CustomerId,
    string CustomerEmail,
    string? CustomerPushToken,
    NotificationType NotificationType,
    string SourceEventMessageId,
    string? DetailsJson = null
) : ICommand<SendCustomerNotificationResult>;

public record SendCustomerNotificationResult(
    bool IsProcessed,
    bool WasDuplicate,
    IReadOnlyCollection<Guid> DispatchedNotificationIds,
    string? ErrorMessage = null
);
