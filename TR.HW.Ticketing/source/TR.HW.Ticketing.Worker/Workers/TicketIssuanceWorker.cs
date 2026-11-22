namespace TR.HW.Ticketing.Worker.Workers;

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TR.HW.Ticketing.Application.Common.Messaging;
using TR.HW.Ticketing.Application.Features.IssueTickets;
using TR.HW.Ticketing.Application.Interfaces;
using TR.HW.Ticketing.Worker.Configuration;
using TR.HW.Ticketing.Worker.Messaging;

public sealed class TicketIssuanceWorker(
    IKafkaConsumer kafkaConsumer,
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaConsumerOptions> options,
    ILogger<TicketIssuanceWorker> logger) : BackgroundService
{
    private static readonly ActivitySource ActivitySource = new("TicketReserver.TicketingWorker", "1.0.0");
    private readonly KafkaConsumerOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Starting TicketIssuanceWorker on topic {Topic} for consumer group {GroupId}",
            _options.Topic,
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
                    "Unexpected error processing Kafka message at partition {Partition}, offset {Offset}",
                    consumeResult?.Partition.Value,
                    consumeResult?.Offset.Value);

                await Task.Delay(1000, stoppingToken);
            }
        }

        logger.LogInformation("TicketIssuanceWorker shutting down");
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

        using var activity = ActivitySource.StartActivity("ProcessTicketIssuance", ActivityKind.Consumer, parentContext);

        var rawMessage = consumeResult.Message.Value;
        var messageKey = consumeResult.Message.Key ?? string.Empty;

        IssueTicketsCommand? command;
        string messageId;

        try
        {
            (command, messageId) = ParsePayload(rawMessage, messageKey, traceparent);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse payment approved message. Routing to DLQ. Raw: {Raw}", rawMessage);
            await kafkaConsumer.SendToDlqAsync(messageKey, rawMessage, $"Payload deserialization error: {ex.Message}", headers, ct);
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITicketingRepository>();

        // Idempotency check in inbox_messages
        if (await repository.IsMessageProcessedAsync(messageId, _options.GroupId, ct))
        {
            logger.LogInformation(
                "Message {MessageId} already processed by group {GroupId}. Skipping duplicate execution",
                messageId,
                _options.GroupId);
            return;
        }

        var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<IssueTicketsCommand, IssueTicketsResult>>();
        var result = await handler.HandleAsync(command, ct);

        logger.LogInformation(
            "Ticket issuance command processed for Reservation {ReservationId}. Result: {Status}, TicketsCount: {Count}",
            command.ReservationId,
            result.Status,
            result.IssuedTickets.Count);
    }

    private static (IssueTicketsCommand Command, string MessageId) ParsePayload(string rawJson, string key, string? traceparent)
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
            messageId = !string.IsNullOrWhiteSpace(key) ? key : Guid.NewGuid().ToString();
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

        var paymentId = TryExtractGuid(dataElement, "paymentId", "PaymentId");
        if (paymentId == Guid.Empty)
        {
            paymentId = Guid.NewGuid();
        }

        var customerId = TryExtractGuid(dataElement, "customerId", "CustomerId");
        if (customerId == Guid.Empty)
        {
            customerId = Guid.NewGuid();
        }

        var catalogEventId = TryExtractGuid(dataElement, "catalogEventId", "CatalogEventId");
        if (catalogEventId == Guid.Empty)
        {
            catalogEventId = TryExtractGuid(dataElement, "eventId", "EventId");
        }
        if (catalogEventId == Guid.Empty)
        {
            catalogEventId = Guid.NewGuid();
        }

        var items = new List<TicketIssuanceItemDto>();
        if (dataElement.TryGetProperty("items", out var itemsProp) && itemsProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var itemEl in itemsProp.EnumerateArray())
            {
                var tierId = TryExtractGuid(itemEl, "ticketTierId", "TicketTierId");
                if (tierId == Guid.Empty) tierId = Guid.NewGuid();

                var seatId = TryExtractNullableGuid(itemEl, "seatId", "SeatId");
                var seatLabel = TryExtractString(itemEl, "seatLabel", "SeatLabel");
                var unitPrice = TryExtractDecimal(itemEl, "unitPrice", "UnitPrice", "price");

                items.Add(new TicketIssuanceItemDto(tierId, seatId, seatLabel, unitPrice));
            }
        }

        if (items.Count == 0)
        {
            var topLevelTierId = TryExtractGuid(dataElement, "ticketTierId", "TicketTierId");
            if (topLevelTierId == Guid.Empty) topLevelTierId = Guid.NewGuid();

            var topLevelSeatId = TryExtractNullableGuid(dataElement, "seatId", "SeatId");
            var topLevelSeatLabel = TryExtractString(dataElement, "seatLabel", "SeatLabel");
            var topLevelAmount = TryExtractDecimal(dataElement, "amount", "Amount", "totalAmount");

            items.Add(new TicketIssuanceItemDto(topLevelTierId, topLevelSeatId, topLevelSeatLabel, topLevelAmount));
        }

        var command = new IssueTicketsCommand(
            ReservationId: reservationId,
            PaymentId: paymentId,
            CustomerId: customerId,
            EventId: catalogEventId,
            Items: items,
            MessageId: messageId,
            Traceparent: traceparent);

        return (command, messageId);
    }

    private static Guid TryExtractGuid(JsonElement element, string camelName, string pascalName)
    {
        if (element.TryGetProperty(camelName, out var prop) && prop.TryGetGuid(out var parsed))
            return parsed;
        if (element.TryGetProperty(pascalName, out var propPascal) && propPascal.TryGetGuid(out var parsedPascal))
            return parsedPascal;
        return Guid.Empty;
    }

    private static Guid? TryExtractNullableGuid(JsonElement element, string camelName, string pascalName)
    {
        if (element.TryGetProperty(camelName, out var prop) && prop.ValueKind != JsonValueKind.Null && prop.TryGetGuid(out var parsed))
            return parsed;
        if (element.TryGetProperty(pascalName, out var propPascal) && propPascal.ValueKind != JsonValueKind.Null && propPascal.TryGetGuid(out var parsedPascal))
            return parsedPascal;
        return null;
    }

    private static string? TryExtractString(JsonElement element, string camelName, string pascalName)
    {
        if (element.TryGetProperty(camelName, out var prop) && prop.ValueKind == JsonValueKind.String)
            return prop.GetString();
        if (element.TryGetProperty(pascalName, out var propPascal) && propPascal.ValueKind == JsonValueKind.String)
            return propPascal.GetString();
        return null;
    }

    private static decimal TryExtractDecimal(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var prop) && prop.TryGetDecimal(out var val))
                return val;
        }
        return 0m;
    }

    private static string? ExtractHeaderString(Headers? headers, string key)
    {
        if (headers is null) return null;
        var header = headers.FirstOrDefault(h => h.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        return header is null ? null : Encoding.UTF8.GetString(header.GetValueBytes());
    }
}
