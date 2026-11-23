using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TR.HW.Notification.Application.Common.Messaging;
using TR.HW.Notification.Application.Features.SendCustomerNotification;
using TR.HW.Notification.Domain.Enums;
using TR.HW.Notification.Worker.Configuration;
using TR.HW.Notification.Worker.Messaging;

namespace TR.HW.Notification.Worker.Workers;

public sealed class NotificationWorker : BackgroundService
{
    private static readonly ActivitySource ActivitySource = new("TicketReserver.Notification", "1.0.0");
    private readonly IKafkaConsumer _kafkaConsumer;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly KafkaConsumerOptions _options;
    private readonly ILogger<NotificationWorker> _logger;

    public NotificationWorker(
        IKafkaConsumer kafkaConsumer,
        IServiceScopeFactory scopeFactory,
        IOptions<KafkaConsumerOptions> options,
        ILogger<NotificationWorker> logger)
    {
        _kafkaConsumer = kafkaConsumer ?? throw new ArgumentNullException(nameof(kafkaConsumer));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Starting NotificationWorker on topics [{Topics}] for consumer group {GroupId}",
            string.Join(", ", _options.Topics),
            _options.GroupId);

        _kafkaConsumer.Subscribe();

        while (!stoppingToken.IsCancellationRequested)
        {
            ConsumeResult<string, string>? consumeResult = null;
            try
            {
                consumeResult = _kafkaConsumer.Consume(TimeSpan.FromMilliseconds(500));
                if (consumeResult is null || consumeResult.IsPartitionEOF)
                {
                    continue;
                }

                await ProcessMessageAsync(consumeResult, stoppingToken);
                _kafkaConsumer.Commit(consumeResult);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unexpected error processing Kafka message at topic {Topic}, partition {Partition}, offset {Offset}",
                    consumeResult?.Topic,
                    consumeResult?.Partition.Value,
                    consumeResult?.Offset.Value);

                await Task.Delay(1000, stoppingToken);
            }
        }

        _logger.LogInformation("NotificationWorker shutting down");
        _kafkaConsumer.Close();
    }

    public async Task ProcessMessageAsync(ConsumeResult<string, string> consumeResult, CancellationToken ct)
    {
        var headers = consumeResult.Message.Headers;
        var traceparent = ExtractHeaderString(headers, "traceparent");

        ActivityContext parentContext = default;
        if (!string.IsNullOrWhiteSpace(traceparent) && ActivityContext.TryParse(traceparent, null, out var parsed))
        {
            parentContext = parsed;
        }

        using var activity = ActivitySource.StartActivity("ProcessCustomerNotification", ActivityKind.Consumer, parentContext);

        var rawMessage = consumeResult.Message.Value;
        var messageKey = consumeResult.Message.Key ?? string.Empty;
        var topic = consumeResult.Topic ?? string.Empty;

        SendCustomerNotificationCommand command;

        try
        {
            command = ParsePayload(rawMessage, messageKey, topic);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to parse notification payload on topic {Topic}. Routing to DLQ. Raw: {Raw}",
                topic,
                rawMessage);

            await _kafkaConsumer.SendToDlqAsync(topic, messageKey, rawMessage, $"Payload deserialization error: {ex.Message}", headers, ct);
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<SendCustomerNotificationCommand, SendCustomerNotificationResult>>();

        var result = await handler.HandleAsync(command, ct);

        if (result.WasDuplicate)
        {
            _logger.LogInformation(
                "Skipped duplicate notification message {MessageId} for reservation {ReservationId}",
                command.SourceEventMessageId,
                command.ReservationId);
        }
        else if (result.IsProcessed)
        {
            _logger.LogInformation(
                "Successfully processed customer notification for reservation {ReservationId}. Dispatched count: {Count}",
                command.ReservationId,
                result.DispatchedNotificationIds.Count);
        }
        else
        {
            _logger.LogWarning(
                "Failed to process customer notification for reservation {ReservationId}: {Error}",
                command.ReservationId,
                result.ErrorMessage);
        }
    }

    public static SendCustomerNotificationCommand ParsePayload(string rawJson, string key, string topic)
    {
        using var doc = JsonDocument.Parse(rawJson);
        var root = doc.RootElement;
        var dataElement = root.TryGetProperty("payload", out var payloadElement) ? payloadElement : root;

        string messageId;
        if (root.TryGetProperty("eventId", out var eventIdProp) && eventIdProp.GetString() is { Length: > 0 } eid)
            messageId = eid;
        else if (root.TryGetProperty("id", out var idProp) && idProp.GetString() is { Length: > 0 } id)
            messageId = id;
        else
            messageId = !string.IsNullOrWhiteSpace(key) ? $"{topic}:{key}" : Guid.NewGuid().ToString();

        var reservationId = TryExtractGuid(dataElement, "reservationId", "ReservationId");
        if (reservationId == Guid.Empty && Guid.TryParse(key, out var keyGuid))
            reservationId = keyGuid;

        if (reservationId == Guid.Empty)
            throw new JsonException("Unable to determine valid ReservationId from message payload or key.");

        var customerId = TryExtractGuid(dataElement, "customerId", "CustomerId");
        if (customerId == Guid.Empty)
            customerId = Guid.NewGuid(); // Fallback if customerId not explicitly emitted

        var customerEmail = TryExtractString(dataElement, "customerEmail", "CustomerEmail")
            ?? TryExtractString(dataElement, "email", "Email");

        if (string.IsNullOrWhiteSpace(customerEmail))
            customerEmail = $"customer_{customerId.ToString("N")[..8]}@ticketreserver.io";

        var pushToken = TryExtractString(dataElement, "customerPushToken", "CustomerPushToken")
            ?? TryExtractString(dataElement, "pushToken", "PushToken");

        var eventTypeStr = TryExtractString(root, "eventType", "EventType")
            ?? TryExtractString(dataElement, "eventType", "EventType")
            ?? topic;

        var notificationType = ResolveNotificationType(topic, eventTypeStr);

        return new SendCustomerNotificationCommand(
            ReservationId: reservationId,
            CustomerId: customerId,
            CustomerEmail: customerEmail,
            CustomerPushToken: pushToken,
            NotificationType: notificationType,
            SourceEventMessageId: messageId,
            DetailsJson: rawJson);
    }

    public static NotificationType ResolveNotificationType(string topic, string eventType)
    {
        var combined = $"{topic} {eventType}".ToLowerInvariant();

        if (combined.Contains("ticketing") && combined.Contains("issued"))
            return NotificationType.TicketIssuedConfirmation;

        if (combined.Contains("refunded"))
            return NotificationType.PaymentRefundedNotice;

        if (combined.Contains("declined"))
            return NotificationType.PaymentDeclinedNotice;

        return NotificationType.TicketIssuedConfirmation;
    }

    private static Guid TryExtractGuid(JsonElement element, string camelName, string pascalName)
    {
        if (element.TryGetProperty(camelName, out var prop) && prop.TryGetGuid(out var parsed))
            return parsed;
        if (element.TryGetProperty(pascalName, out var propPascal) && propPascal.TryGetGuid(out var parsedPascal))
            return parsedPascal;
        return Guid.Empty;
    }

    private static string? TryExtractString(JsonElement element, string camelName, string pascalName)
    {
        if (element.TryGetProperty(camelName, out var prop) && prop.ValueKind == JsonValueKind.String)
            return prop.GetString();
        if (element.TryGetProperty(pascalName, out var propPascal) && propPascal.ValueKind == JsonValueKind.String)
            return propPascal.GetString();
        return null;
    }

    private static string? ExtractHeaderString(Headers? headers, string key)
    {
        if (headers is null) return null;
        var header = headers.FirstOrDefault(h => h.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        return header is null ? null : Encoding.UTF8.GetString(header.GetValueBytes());
    }
}
