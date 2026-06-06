using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TR.HW.OutboxRelay.Application.Ports;

namespace TR.HW.OutboxRelay.Infrastructure.Adapters.Kafka;

public class KafkaAdapter : IKafkaPort, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaAdapter> _logger;

    public KafkaAdapter(IConfiguration configuration, ILogger<KafkaAdapter> logger)
    {
        _logger = logger;
        var bootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092";
        
        var config = new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            Acks = Acks.All,
            MessageSendMaxRetries = 3,
            RetryBackoffMs = 1000,
            EnableIdempotence = true
        };

        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public async Task PublishAsync(string topic, string key, string payload, string correlationId)
    {
        var headers = new Headers
        {
            { "traceparent", Encoding.UTF8.GetBytes(correlationId) }
        };

        var message = new Message<string, string>
        {
            Key = key,
            Value = payload,
            Headers = headers
        };

        var result = await _producer.ProduceAsync(topic, message);
        
        _logger.LogInformation("Message published to {Topic} (Partition: {Partition}, Offset: {Offset})", 
            result.Topic, result.Partition.Value, result.Offset.Value);
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(10));
        _producer.Dispose();
    }
}
