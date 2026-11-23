using Microsoft.Extensions.Logging;

namespace TR.HW.Notification.Infrastructure.Gateways;

public sealed class SimulatedNotificationProviderGateway : INotificationProviderGateway
{
    private readonly ILogger<SimulatedNotificationProviderGateway> _logger;

    public SimulatedNotificationProviderGateway(ILogger<SimulatedNotificationProviderGateway> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<string> SendEmailAsync(
        string recipientEmail,
        string title,
        string content,
        string? metadataJson = null,
        CancellationToken cancellationToken = default)
    {
        var reference = $"SIM-EMAIL-{Guid.NewGuid():N}";
        _logger.LogInformation(
            "Simulated Email sent to {Recipient}. Title: '{Title}', Ref: {Reference}",
            recipientEmail,
            title,
            reference);

        return Task.FromResult(reference);
    }

    public Task<string> SendPushAsync(
        string deviceToken,
        string title,
        string content,
        string? metadataJson = null,
        CancellationToken cancellationToken = default)
    {
        var reference = $"SIM-PUSH-{Guid.NewGuid():N}";
        _logger.LogInformation(
            "Simulated Push sent to token {TokenPrefix}... Title: '{Title}', Ref: {Reference}",
            deviceToken.Length > 8 ? deviceToken[..8] : deviceToken,
            title,
            reference);

        return Task.FromResult(reference);
    }
}
