namespace TR.HW.ResTimeout.Workers;

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TR.HW.ResTimeout.Caching;
using TR.HW.ResTimeout.Configuration;
using TR.HW.ResTimeout.Messaging;
using TR.HW.ResTimeout.Persistence;

public sealed class ReservationTimeoutWorker(
    IKafkaConsumer kafkaConsumer,
    IReservationTimeoutRepository repository,
    ISeatLockManager seatLockManager,
    IOptions<KafkaConsumerOptions> options,
    ILogger<ReservationTimeoutWorker> logger) : BackgroundService
{
    private static readonly ActivitySource ActivitySource = new("TicketReserver.ResTimeout", "1.0.0");
    private readonly KafkaConsumerOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting ReservationTimeoutWorker on topic {Topic} for consumer group {GroupId}", _options.Topic, _options.GroupId);
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
                logger.LogError(ex, "Unexpected error processing Kafka message at offset {Offset}", consumeResult?.Offset);
                await Task.Delay(1000, stoppingToken);
            }
        }

        logger.LogInformation("ReservationTimeoutWorker shutting down");
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

        using var activity = ActivitySource.StartActivity("ProcessReservationTimeout", ActivityKind.Consumer, parentContext);

        var messageValue = consumeResult.Message.Value;
        Guid reservationId;
        string messageId;

        try
        {
            using var doc = JsonDocument.Parse(messageValue);
            var root = doc.RootElement;

            if (root.TryGetProperty("aggregateId", out var aggIdProp) && aggIdProp.TryGetGuid(out var parsedAggId))
            {
                reservationId = parsedAggId;
            }
            else if (root.TryGetProperty("AggregateId", out var aggIdPropPascal) && aggIdPropPascal.TryGetGuid(out var parsedAggIdPascal))
            {
                reservationId = parsedAggIdPascal;
            }
            else if (root.TryGetProperty("reservationId", out var resIdProp) && resIdProp.TryGetGuid(out var parsedResId))
            {
                reservationId = parsedResId;
            }
            else if (Guid.TryParse(consumeResult.Message.Key, out var keyGuid))
            {
                reservationId = keyGuid;
            }
            else
            {
                throw new JsonException("Unable to find valid reservation identifier (aggregateId or key) in Kafka message.");
            }

            if (root.TryGetProperty("id", out var idProp))
            {
                messageId = idProp.GetString() ?? consumeResult.Message.Key ?? reservationId.ToString();
            }
            else if (root.TryGetProperty("Id", out var idPropPascal))
            {
                messageId = idPropPascal.GetString() ?? consumeResult.Message.Key ?? reservationId.ToString();
            }
            else
            {
                messageId = ExtractHeaderString(headers, "correlation_id") ?? consumeResult.Message.Key ?? reservationId.ToString();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse JSON payload for message with key '{Key}'", consumeResult.Message.Key);
            await kafkaConsumer.SendToDlqAsync(
                consumeResult.Message.Key ?? string.Empty,
                messageValue,
                $"Deserialization error: {ex.Message}",
                headers,
                ct);
            return;
        }

        // Idempotency check via inbox
        if (await repository.IsMessageProcessedAsync(messageId, _options.GroupId, ct))
        {
            logger.LogInformation("Message {MessageId} already processed for consumer group {GroupId}, skipping", messageId, _options.GroupId);
            return;
        }

        var reservation = await repository.GetReservationAsync(reservationId, ct);
        if (reservation is null)
        {
            logger.LogWarning("Reservation {ReservationId} not found in database. Marking inbox message as processed", reservationId);
            await repository.RecordProcessedMessageAsync(messageId, _options.GroupId, "reservation.v1.hold_created.delayed", ct);
            return;
        }

        if (string.Equals(reservation.Status, "AWAITING_PAYMENT", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("Expiring reservation {ReservationId} due to payment timeout", reservationId);
            var expired = await repository.ExpireReservationAsync(reservationId, messageId, _options.GroupId, ct);

            if (expired && reservation.Items.Count > 0)
            {
                var seatIds = reservation.Items.Select(i => i.SeatId).Distinct();
                await seatLockManager.ReleaseSeatLocksAsync(reservation.EventId, seatIds, reservation.Id, ct);
            }
        }
        else
        {
            logger.LogInformation("Reservation {ReservationId} is currently '{Status}'. Timeout expiration skipped", reservationId, reservation.Status);
            await repository.RecordProcessedMessageAsync(messageId, _options.GroupId, "reservation.v1.hold_created.delayed", ct);
        }
    }

    private static string? ExtractHeaderString(Headers? headers, string key)
    {
        if (headers is null) return null;
        var header = headers.FirstOrDefault(h => string.Equals(h.Key, key, StringComparison.OrdinalIgnoreCase));
        return header is not null ? Encoding.UTF8.GetString(header.GetValueBytes()) : null;
    }
}
