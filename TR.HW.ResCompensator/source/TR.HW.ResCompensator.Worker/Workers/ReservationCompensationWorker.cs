namespace TR.HW.ResCompensator.Worker.Workers;

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TR.HW.ResCompensator.Application.Common.Messaging;
using TR.HW.ResCompensator.Application.Features.ReconcileReservation;
using TR.HW.ResCompensator.Application.Interfaces;
using TR.HW.ResCompensator.Domain.Enums;
using TR.HW.ResCompensator.Worker.Configuration;
using TR.HW.ResCompensator.Worker.Messaging;

public sealed class ReservationCompensationWorker(
    IKafkaConsumer kafkaConsumer,
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaConsumerOptions> options,
    ILogger<ReservationCompensationWorker> logger) : BackgroundService
{
    private static readonly ActivitySource ActivitySource = new("TicketReserver.ResCompensator", "1.0.0");
    private readonly KafkaConsumerOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Starting ReservationCompensationWorker on topics [{Topics}] for consumer group {GroupId}",
            string.Join(", ", _options.Topics),
            _options.GroupId);

        kafkaConsumer.Subscribe();

        while (!stoppingToken.IsCancellationRequested)
        {
            ConsumeResult<string, string>? consumeResult = null;
            try
            {
                consumeResult = kafkaConsumer.Consume(TimeSpan.FromMilliseconds(500));
                if (consumeResult is null || consumeResult.IsPartitionEOF)
                {
                    continue;
                }

                await ProcessMessageAsync(consumeResult, stoppingToken);
                kafkaConsumer.Commit(consumeResult);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Unexpected error processing Kafka message at topic {Topic}, partition {Partition}, offset {Offset}",
                    consumeResult?.Topic,
                    consumeResult?.Partition.Value,
                    consumeResult?.Offset.Value);

                await Task.Delay(1000, stoppingToken);
            }
        }

        logger.LogInformation("ReservationCompensationWorker shutting down");
        kafkaConsumer.Close();
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

        using var activity = ActivitySource.StartActivity("ProcessReservationCompensation", ActivityKind.Consumer, parentContext);

        var rawMessage = consumeResult.Message.Value;
        var messageKey = consumeResult.Message.Key ?? string.Empty;
        var topic = consumeResult.Topic ?? string.Empty;

        ReconcileReservationOutcomeCommand? command;
        string messageId;

        try
        {
            (command, messageId) = ParsePayload(rawMessage, messageKey, topic, traceparent);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse saga outcome message on topic {Topic}. Routing to DLQ. Raw: {Raw}", topic, rawMessage);
            await kafkaConsumer.SendToDlqAsync(messageKey, rawMessage, $"Payload deserialization error: {ex.Message}", headers, ct);
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IReservationCompensationRepository>();

        if (await repository.IsMessageProcessedAsync(messageId, _options.GroupId, ct))
        {
            logger.LogInformation(
                "Message {MessageId} already processed by group {GroupId}. Skipping duplicate execution",
                messageId,
                _options.GroupId);
            return;
        }

        var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<ReconcileReservationOutcomeCommand, ReconcileReservationOutcomeResult>>();
        var result = await handler.HandleAsync(command, ct);
        TicketReserver.Diagnostics.TicketReserverDiagnostics.RecordSagaEvent("reservation.compensated");

        logger.LogInformation(
            "Reservation compensation processed for Reservation {ReservationId}. FinalStatus: {FinalStatus}, ReleasedSeats: {ReleasedSeats}",
            command.ReservationId,
            result.FinalStatus,
            result.ReleasedRedisSeatsCount);
    }

    private static (ReconcileReservationOutcomeCommand Command, string MessageId) ParsePayload(
        string rawJson,
        string key,
        string topic,
        string? traceparent)
    {
        using var doc = JsonDocument.Parse(rawJson);
        var root = doc.RootElement;

        var dataElement = root.TryGetProperty("payload", out var payloadElement) ? payloadElement : root;

        string messageId;
        if (root.TryGetProperty("eventId", out var eventIdProp) && eventIdProp.GetString() is { Length: > 0 } eid)
        {
            messageId = eid;
        }
        else if (root.TryGetProperty("id", out var idProp) && idProp.GetString() is { Length: > 0 } id)
        {
            messageId = id;
        }
        else
        {
            messageId = !string.IsNullOrWhiteSpace(key) ? $"{topic}:{key}" : Guid.NewGuid().ToString();
        }

        var reservationId = TryExtractGuid(dataElement, "reservationId", "ReservationId");
        if (reservationId == Guid.Empty && Guid.TryParse(key, out var keyGuid))
        {
            reservationId = keyGuid;
        }

        if (reservationId == Guid.Empty)
        {
            throw new JsonException("Unable to determine valid ReservationId from message payload or key.");
        }

        var eventTypeStr = TryExtractString(root, "eventType", "EventType")
            ?? TryExtractString(dataElement, "eventType", "EventType")
            ?? topic;

        var (outcomeType, normalizedEventType, defaultReason) = ResolveOutcomeType(topic, eventTypeStr);

        var customReason = TryExtractString(dataElement, "reason", "Reason")
            ?? TryExtractString(dataElement, "failureReason", "FailureReason");

        string? resolvedReason = outcomeType switch
        {
            ReservationOutcomeType.CancelledByRefund => "AUTO_REFUND_SEAT_COLLISION",
            ReservationOutcomeType.CancelledByDecline => !string.IsNullOrWhiteSpace(customReason) ? customReason : defaultReason,
            _ => null
        };

        var command = new ReconcileReservationOutcomeCommand(
            ReservationId: reservationId,
            OutcomeType: outcomeType,
            EventType: normalizedEventType,
            Reason: resolvedReason,
            MessageId: messageId,
            Traceparent: traceparent);

        return (command, messageId);
    }

    private static (ReservationOutcomeType Outcome, string EventType, string? DefaultReason) ResolveOutcomeType(string topic, string eventType)
    {
        var combined = $"{topic} {eventType}".ToLowerInvariant();

        if (combined.Contains("ticketing") && combined.Contains("issued"))
        {
            return (ReservationOutcomeType.Confirmed, "ticketing.v1.issued", null);
        }

        if (combined.Contains("declined"))
        {
            return (ReservationOutcomeType.CancelledByDecline, "payment.v1.declined", "PAYMENT_DECLINED");
        }

        return (ReservationOutcomeType.CancelledByRefund, "payment.v1.refunded", "AUTO_REFUND_SEAT_COLLISION");
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
