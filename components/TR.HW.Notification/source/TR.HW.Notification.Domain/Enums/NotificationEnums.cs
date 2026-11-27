namespace TR.HW.Notification.Domain.Enums;

public enum NotificationType
{
    TicketIssuedConfirmation = 1,
    PaymentRefundedNotice = 2,
    PaymentDeclinedNotice = 3
}

public enum NotificationChannel
{
    Email = 1,
    Push = 2
}

public enum NotificationStatus
{
    Pending = 1,
    Sent = 2,
    Failed = 3
}
