namespace TR.HW.Notification.Infrastructure.Gateways;

public interface INotificationProviderGateway
{
    Task<string> SendEmailAsync(
        string recipientEmail,
        string title,
        string content,
        string? metadataJson = null,
        CancellationToken cancellationToken = default);

    Task<string> SendPushAsync(
        string deviceToken,
        string title,
        string content,
        string? metadataJson = null,
        CancellationToken cancellationToken = default);
}
