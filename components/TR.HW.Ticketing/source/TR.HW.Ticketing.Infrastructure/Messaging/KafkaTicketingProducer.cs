namespace TR.HW.Ticketing.Infrastructure.Messaging;

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TR.HW.Ticketing.Application.Interfaces;
using TR.HW.Ticketing.Domain.Events;
using TR.HW.Ticketing.Infrastructure.Configuration;

public sealed class KafkaTicketingProducer : IKafkaTicketingProducer, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly KafkaProducerOptions _options;
    private readonly ILogger<KafkaTicketingProducer> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly bool _ownsProducer;

    public KafkaTicketingProducer(
        IOptions<KafkaProducerOptions> options,
        ILogger<KafkaTicketingProducer> logger,
        IProducer<string, string>? producer = null)
    {
        _options = options.Value;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        if (producer != null)
        {
            _producer = producer;
            _ownsProducer = false;
        }
        else
        {
            var config = new ProducerConfig
            {
                BootstrapServers = _options.BootstrapServers,
                ClientId = _options.ClientId,
                Acks = Acks.All,
                EnableIdempotence = true,
                MessageTimeoutMs = _options.MessageTimeoutMs
            };

            _producer = new ProducerBuilder<string, string>(config).Build();
            _ownsProducer = true;
        }
    }

    public async Task PublishTicketsIssuedAsync(
        Guid reservationId,
        Guid paymentId,
        Guid customerId,
        Guid eventId,
        IReadOnlyList<TicketIssuedDomainEvent> ticketEvents,
        string? traceparent = null,
        CancellationToken ct = default)
    {
        var topic = _options.TicketsIssuedTopic;
        var key = reservationId.ToString();

        var payload = new
        {
            eventId = Guid.NewGuid(),
            eventType = "ticketing.v1.issued",
            reservationId,
            paymentId,
            customerId,
            catalogEventId = eventId,
            tickets = ticketEvents.Select(t => new
            {
                ticketId = t.TicketId,
                ticketCode = t.TicketCode,
                qrCodeHash = t.QrCodeHash,
                ticketTierId = t.TicketTierId,
                seatId = t.SeatId,
                seatLabel = t.SeatLabel,
                pricePaid = t.PricePaid,
                issuedAt = t.OccurredOn
            }),
            occurredOn = DateTime.UtcNow
        };

        await PublishMessageAsync(topic, key, payload, traceparent, ct);
    }

    public async Task PublishTicketingFailedAsync(
        TicketingFailedDomainEvent domainEvent,
        string? traceparent = null,
        CancellationToken ct = default)
    {
        var topic = _options.TicketingFailedTopic;
        var key = domainEvent.ReservationId.ToString();

        var payload = new
        {
            eventId = Guid.NewGuid(),
            eventType = "ticketing.v1.failed",
            reservationId = domainEvent.ReservationId,
            paymentId = domainEvent.PaymentId,
            customerId = domainEvent.CustomerId,
            catalogEventId = domainEvent.EventId,
            failureReason = domainEvent.FailureReason,
            occurredOn = domainEvent.OccurredOn
        };

        await PublishMessageAsync(topic, key, payload, traceparent, ct);
    }

    private async Task PublishMessageAsync<T>(
        string topic,
        string key,
        T messagePayload,
        string? traceparent,
        CancellationToken ct)
    {
        var headers = new Headers();
        var resolvedTraceparent = traceparent ?? Activity.Current?.Id ?? $"00-{Guid.NewGuid():N}-{Guid.NewGuid().ToString("N")[..16]}-01";

        headers.Add("traceparent", Encoding.UTF8.GetBytes(resolvedTraceparent));
        headers.Add("produced_at", Encoding.UTF8.GetBytes(DateTimeOffset.UtcNow.ToString("O")));

        var json = JsonSerializer.Serialize(messagePayload, _jsonOptions);

        var message = new Message<string, string>
        {
            Key = key,
            Value = json,
            Headers = headers,
            Timestamp = new Timestamp(DateTime.UtcNow)
        };

        try
        {
            var deliveryResult = await _producer.ProduceAsync(topic, message, ct);
            _logger.LogInformation(
                "Produced ticketing event to topic {Topic} [partition: {Partition}, offset: {Offset}] for key {Key}",
                deliveryResult.Topic,
                deliveryResult.Partition.Value,
                deliveryResult.Offset.Value,
                key);
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(
                ex,
                "Failed to produce message to {Topic} for key {Key}. Error code: {ErrorCode}. Attempting DLQ fallback",
                topic,
                key,
                ex.Error.Code);

            await SendToDlqFallbackAsync(key, json, headers, ex.Message, ct);
            throw;
        }
    }

    private async Task SendToDlqFallbackAsync(
        string key,
        string payload,
        Headers originalHeaders,
        string errorMessage,
        CancellationToken ct)
    {
        try
        {
            var dlqHeaders = new Headers();
            foreach (var h in originalHeaders)
            {
                dlqHeaders.Add(h.Key, h.GetValueBytes());
            }
            dlqHeaders.Add("dlq_error", Encoding.UTF8.GetBytes(errorMessage));

            var dlqMessage = new Message<string, string>
            {
                Key = key,
                Value = payload,
                Headers = dlqHeaders,
                Timestamp = new Timestamp(DateTime.UtcNow)
            };

            await _producer.ProduceAsync(_options.DeadLetterQueueTopic, dlqMessage, ct);
            _logger.LogWarning("Message sent to DLQ topic {DlqTopic} for key {Key}", _options.DeadLetterQueueTopic, key);
        }
        catch (Exception dlqEx)
        {
            _logger.LogCritical(dlqEx, "Failed to send message to DLQ topic {DlqTopic} for key {Key}", _options.DeadLetterQueueTopic, key);
        }
    }

    public void Dispose()
    {
        if (_ownsProducer)
        {
            _producer.Flush(TimeSpan.FromSeconds(5));
            _producer.Dispose();
        }
    }
}
