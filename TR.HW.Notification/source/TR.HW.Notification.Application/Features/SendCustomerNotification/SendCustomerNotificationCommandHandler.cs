using FluentValidation;
using Microsoft.Extensions.Logging;
using TR.HW.Notification.Application.Common.Messaging;
using TR.HW.Notification.Application.Interfaces;

namespace TR.HW.Notification.Application.Features.SendCustomerNotification;

public sealed class SendCustomerNotificationCommandHandler : ICommandHandler<SendCustomerNotificationCommand, SendCustomerNotificationResult>
{
    private const string ConsumerGroup = "cg-notification-worker";
    private readonly INotificationInboxStore _inboxStore;
    private readonly INotificationDispatcher _dispatcher;
    private readonly IValidator<SendCustomerNotificationCommand> _validator;
    private readonly ILogger<SendCustomerNotificationCommandHandler> _logger;

    public SendCustomerNotificationCommandHandler(
        INotificationInboxStore inboxStore,
        INotificationDispatcher dispatcher,
        IValidator<SendCustomerNotificationCommand> validator,
        ILogger<SendCustomerNotificationCommandHandler> logger)
    {
        _inboxStore = inboxStore ?? throw new ArgumentNullException(nameof(inboxStore));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SendCustomerNotificationResult> HandleAsync(
        SendCustomerNotificationCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
            _logger.LogWarning("Validation failed for notification command: {Errors}", errors);
            return new SendCustomerNotificationResult(
                IsProcessed: false,
                WasDuplicate: false,
                DispatchedNotificationIds: Array.Empty<Guid>(),
                ErrorMessage: errors);
        }

        // 1. Inbox Deduplication Check
        var alreadyProcessed = await _inboxStore.HasBeenProcessedAsync(
            ConsumerGroup,
            command.SourceEventMessageId,
            cancellationToken);

        if (alreadyProcessed)
        {
            _logger.LogInformation(
                "Notification message {MessageId} for reservation {ReservationId} already processed by inbox. Skipping dispatch.",
                command.SourceEventMessageId,
                command.ReservationId);

            return new SendCustomerNotificationResult(
                IsProcessed: true,
                WasDuplicate: true,
                DispatchedNotificationIds: Array.Empty<Guid>());
        }

        // 2. Build multi-channel notifications
        var notifications = CustomerNotificationMappingExtensions.BuildNotifications(command);
        var dispatchedIds = new List<Guid>();

        foreach (var notification in notifications)
        {
            try
            {
                var result = await _dispatcher.DispatchAsync(notification, cancellationToken);
                if (result.IsSuccess)
                {
                    notification.MarkAsSent(result.ProviderReference ?? $"SIM-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
                    dispatchedIds.Add(notification.Id);
                    _logger.LogInformation(
                        "Dispatched {Channel} notification {NotificationId} for reservation {ReservationId}. Type: {Type}",
                        notification.Channel,
                        notification.Id,
                        notification.ReservationId,
                        notification.Type);
                }
                else
                {
                    notification.MarkAsFailed(result.ErrorMessage ?? "Unknown provider failure", DateTimeOffset.UtcNow);
                    _logger.LogWarning(
                        "Failed to dispatch {Channel} notification {NotificationId} for reservation {ReservationId}: {Error}",
                        notification.Channel,
                        notification.Id,
                        notification.ReservationId,
                        result.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                notification.MarkAsFailed(ex.Message, DateTimeOffset.UtcNow);
                _logger.LogError(
                    ex,
                    "Unexpected exception dispatching {Channel} notification {NotificationId} for reservation {ReservationId}",
                    notification.Channel,
                    notification.Id,
                    notification.ReservationId);
            }
        }

        // 3. Mark processed in distributed inbox store
        await _inboxStore.MarkAsProcessedAsync(
            ConsumerGroup,
            command.SourceEventMessageId,
            cancellationToken: cancellationToken);

        return new SendCustomerNotificationResult(
            IsProcessed: true,
            WasDuplicate: false,
            DispatchedNotificationIds: dispatchedIds);
    }
}
