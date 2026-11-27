namespace TR.HW.ReservationOutboxRelay.Messaging;

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TR.HW.ReservationOutboxRelay.Configuration;
using TR.HW.ReservationOutboxRelay.Persistence;

public class KafkaProducer : IKafkaProducer
{
    private readonly KafkaProducerOptions _options;
    private readonly ILogger<KafkaProducer> _logger;
    private readonly IProducer<string, string> _producer;
    private readonly bool _ownsProducer;

    public KafkaProducer(
        IOptions<KafkaProducerOptions> options,
        ILogger<KafkaProducer> logger,
        IProducer<string, string>? producer = null)
    {
        _options = options.Value;
        _logger = logger;

        if (producer is not null)
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

    public string ResolveTopic(string eventType) => eventType switch
    {
        "reservation.checkout_req" => _options.CheckoutTopic,
        "reservation.hold_created" => _options.HoldCreatedDelayedTopic,
        _ => _options.DeadLetterQueueTopic
    };

    public async Task ProduceAsync(OutboxMessageRecord message, CancellationToken cancellationToken = default)
    {
        var topic = ResolveTopic(message.EventType);
        var envelope = new OutboxEventEnvelope(
            message.Id,
            message.CorrelationId,
            message.EventType,
            message.AggregateId,
            DateTime.UtcNow,
            message.Payload);

        var json = JsonSerializer.Serialize(envelope);
        var kafkaMessage = new Message<string, string>
        {
            Key = message.AggregateId.ToString(),
            Value = json,
            Headers = CreateHeaders(message)
        };

        _logger.LogInformation(
            "Despachando evento outbox {EventId} tipo '{EventType}' para tópico '{Topic}', Key: {Key}",
            message.Id, message.EventType, topic, kafkaMessage.Key);

        var deliveryResult = await _producer.ProduceAsync(topic, kafkaMessage, cancellationToken);

        if (deliveryResult.Status == PersistenceStatus.NotPersisted)
        {
            throw new InvalidOperationException($"Falha ao persistir evento {message.Id} no Kafka (Status: NotPersisted)");
        }
    }

    public async Task ProduceToDeadLetterQueueAsync(OutboxMessageRecord message, string reason, CancellationToken cancellationToken = default)
    {
        var topic = _options.DeadLetterQueueTopic;
        var envelope = new OutboxEventEnvelope(
            message.Id,
            message.CorrelationId,
            message.EventType,
            message.AggregateId,
            DateTime.UtcNow,
            message.Payload);

        var json = JsonSerializer.Serialize(envelope);
        var headers = CreateHeaders(message);
        headers.Add("dlq_reason", Encoding.UTF8.GetBytes(reason));

        var kafkaMessage = new Message<string, string>
        {
            Key = message.AggregateId.ToString(),
            Value = json,
            Headers = headers
        };

        _logger.LogWarning(
            "Enviando evento outbox {EventId} para DLQ '{Topic}' devido a: {Reason}",
            message.Id, topic, reason);

        await _producer.ProduceAsync(topic, kafkaMessage, cancellationToken);
    }

    private static Headers CreateHeaders(OutboxMessageRecord message)
    {
        var headers = new Headers
        {
            { "correlation_id", Encoding.UTF8.GetBytes(message.CorrelationId.ToString()) },
            { "event_type", Encoding.UTF8.GetBytes(message.EventType) },
            { "aggregate_id", Encoding.UTF8.GetBytes(message.AggregateId.ToString()) }
        };

        var traceParent = Activity.Current?.Id ?? $"00-{Guid.NewGuid():N}-{Guid.NewGuid():N}".Substring(0, 53) + "-01";
        headers.Add("traceparent", Encoding.UTF8.GetBytes(traceParent));

        return headers;
    }

    public void Dispose()
    {
        if (_ownsProducer)
        {
            _producer.Flush(TimeSpan.FromSeconds(5));
            _producer.Dispose();
        }
        GC.SuppressFinalize(this);
    }
}
