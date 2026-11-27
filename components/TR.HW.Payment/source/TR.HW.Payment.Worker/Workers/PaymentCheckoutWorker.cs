namespace TR.HW.Payment.Worker.Workers;

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TR.HW.Payment.Application.Common.Messaging;
using TR.HW.Payment.Application.Features.ProcessPayment;
using TR.HW.Payment.Application.Interfaces;
using TR.HW.Payment.Worker.Configuration;
using TR.HW.Payment.Worker.Messaging;

public sealed class PaymentCheckoutWorker(
    IKafkaConsumer kafkaConsumer,
    IServiceScopeFactory scopeFactory,
    IOptions<KafkaConsumerOptions> options,
    ILogger<PaymentCheckoutWorker> logger) : BackgroundService
{
    private static readonly ActivitySource ActivitySource = new("TicketReserver.PaymentWorker", "1.0.0");
    private readonly KafkaConsumerOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Starting PaymentCheckoutWorker on topic {Topic} for consumer group {GroupId}",
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

        logger.LogInformation("PaymentCheckoutWorker shutting down");
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

        using var activity = ActivitySource.StartActivity("ProcessPaymentCheckout", ActivityKind.Consumer, parentContext);

        var rawMessage = consumeResult.Message.Value;
        var messageKey = consumeResult.Message.Key ?? string.Empty;

        ProcessPaymentCommand? command;
        string messageId;

        try
        {
            (command, messageId) = ParsePayload(rawMessage, messageKey, traceparent);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to parse checkout request message. Routing to DLQ. Raw: {Raw}", rawMessage);
            await kafkaConsumer.SendToDlqAsync(messageKey, rawMessage, $"Payload deserialization error: {ex.Message}", headers, ct);
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPaymentRepository>();

        // Idempotência por Inbox: verifica se a mensagem já foi consumida por este grupo
        if (await repository.IsMessageProcessedAsync(messageId, _options.GroupId, ct))
        {
            logger.LogInformation(
                "Message {MessageId} already processed by group {GroupId}. Skipping duplicate execution",
                messageId,
                _options.GroupId);
            return;
        }

        var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<ProcessPaymentCommand, ProcessPaymentResult>>();
        var result = await handler.HandleAsync(command, ct);

        if (result.IsApproved)
        {
            TicketReserver.Diagnostics.TicketReserverDiagnostics.RecordSagaEvent("payment.v1.approved");
        }
        else
        {
            TicketReserver.Diagnostics.TicketReserverDiagnostics.RecordSagaEvent("payment.v1.refunded", "failed");
        }

        logger.LogInformation(
            "Payment checkout command processed for Reservation {ReservationId}. Result: {Status}, PaymentId: {PaymentId}",
            command.ReservationId,
            result.Status,
            result.PaymentId);
    }

    private static (ProcessPaymentCommand Command, string MessageId) ParsePayload(string rawJson, string key, string? traceparent)
    {
        using var doc = JsonDocument.Parse(rawJson);
        var root = doc.RootElement;

        // Suporte para envelope com propriedade interna 'payload' ou objeto direto
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

        Guid reservationId = Guid.Empty;
        if (dataElement.TryGetProperty("reservationId", out var resIdProp) && resIdProp.TryGetGuid(out var parsedResId))
        {
            reservationId = parsedResId;
        }
        else if (dataElement.TryGetProperty("ReservationId", out var resIdPropPascal) && resIdPropPascal.TryGetGuid(out var parsedResIdPascal))
        {
            reservationId = parsedResIdPascal;
        }
        else if (Guid.TryParse(key, out var keyGuid))
        {
            reservationId = keyGuid;
        }

        if (reservationId == Guid.Empty)
        {
            throw new JsonException("Unable to determine valid ReservationId from message payload or key.");
        }

        Guid customerId = Guid.Empty;
        if (dataElement.TryGetProperty("customerId", out var custIdProp) && custIdProp.TryGetGuid(out var parsedCustId))
        {
            customerId = parsedCustId;
        }
        else if (dataElement.TryGetProperty("CustomerId", out var custIdPropPascal) && custIdPropPascal.TryGetGuid(out var parsedCustIdPascal))
        {
            customerId = parsedCustIdPascal;
        }

        if (customerId == Guid.Empty)
        {
            customerId = Guid.NewGuid();
        }

        decimal totalAmount = 0;
        if (dataElement.TryGetProperty("totalAmount", out var amountProp) && amountProp.TryGetDecimal(out var parsedAmount))
        {
            totalAmount = parsedAmount;
        }
        else if (dataElement.TryGetProperty("TotalAmount", out var amountPropPascal) && amountPropPascal.TryGetDecimal(out var parsedAmountPascal))
        {
            totalAmount = parsedAmountPascal;
        }
        else if (dataElement.TryGetProperty("amount", out var amountSimple) && amountSimple.TryGetDecimal(out var parsedAmountSimple))
        {
            totalAmount = parsedAmountSimple;
        }

        var currency = "BRL";
        if (dataElement.TryGetProperty("currency", out var currProp) && currProp.GetString() is { Length: > 0 } curr)
        {
            currency = curr;
        }

        var paymentMethod = "CREDIT_CARD";
        if (dataElement.TryGetProperty("paymentMethod", out var pmProp) && pmProp.GetString() is { Length: > 0 } pm)
        {
            paymentMethod = pm;
        }

        string? cardLastFour = null;
        if (dataElement.TryGetProperty("cardLastFour", out var clfProp))
        {
            cardLastFour = clfProp.GetString();
        }

        string? cardBrand = null;
        if (dataElement.TryGetProperty("cardBrand", out var cbProp))
        {
            cardBrand = cbProp.GetString();
        }

        var command = new ProcessPaymentCommand(
            ReservationId: reservationId,
            CustomerId: customerId,
            TotalAmount: totalAmount,
            Currency: currency,
            PaymentMethod: paymentMethod,
            CardLastFour: cardLastFour,
            CardBrand: cardBrand,
            MessageId: messageId,
            Traceparent: traceparent);

        return (command, messageId);
    }

    private static string? ExtractHeaderString(Headers? headers, string key)
    {
        if (headers is null) return null;
        var header = headers.FirstOrDefault(h => h.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        return header is null ? null : Encoding.UTF8.GetString(header.GetValueBytes());
    }
}
