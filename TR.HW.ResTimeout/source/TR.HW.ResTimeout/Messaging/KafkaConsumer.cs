namespace TR.HW.ResTimeout.Messaging;

using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TR.HW.ResTimeout.Configuration;

public class KafkaConsumer : IKafkaConsumer
{
    private readonly KafkaConsumerOptions _options;
    private readonly ILogger<KafkaConsumer> _logger;
    private readonly IConsumer<string, string> _consumer;
    private readonly IProducer<string, string>? _dlqProducer;
    private readonly bool _ownsConsumer;
    private readonly bool _ownsProducer;
    private bool _disposed;

    public KafkaConsumer(
        IOptions<KafkaConsumerOptions> options,
        ILogger<KafkaConsumer> logger,
        IConsumer<string, string>? consumer = null,
        IProducer<string, string>? dlqProducer = null)
    {
        _options = options.Value;
        _logger = logger;

        if (consumer is not null)
        {
            _consumer = consumer;
            _ownsConsumer = false;
        }
        else
        {
            var config = new ConsumerConfig
            {
                BootstrapServers = _options.BootstrapServers,
                GroupId = _options.GroupId,
                AutoOffsetReset = Enum.TryParse<AutoOffsetReset>(_options.AutoOffsetReset, true, out var offsetReset) ? offsetReset : AutoOffsetReset.Earliest,
                EnableAutoCommit = _options.EnableAutoCommit,
                SessionTimeoutMs = _options.SessionTimeoutMs,
                MaxPollIntervalMs = _options.MaxPollIntervalMs,
                EnablePartitionEof = false
            };

            _consumer = new ConsumerBuilder<string, string>(config).Build();
            _ownsConsumer = true;
        }

        if (dlqProducer is not null)
        {
            _dlqProducer = dlqProducer;
            _ownsProducer = false;
        }
        else if (!string.IsNullOrWhiteSpace(_options.BootstrapServers))
        {
            var producerConfig = new ProducerConfig
            {
                BootstrapServers = _options.BootstrapServers,
                Acks = Acks.All,
                ClientId = $"{_options.GroupId}-dlq-producer"
            };

            _dlqProducer = new ProducerBuilder<string, string>(producerConfig).Build();
            _ownsProducer = true;
        }
    }

    public void Subscribe()
    {
        _logger.LogInformation("Subscribing to topic {Topic} with group {GroupId}", _options.Topic, _options.GroupId);
        _consumer.Subscribe(_options.Topic);
    }

    public ConsumeResult<string, string>? Consume(TimeSpan timeout)
    {
        try
        {
            return _consumer.Consume(timeout);
        }
        catch (ConsumeException ex)
        {
            _logger.LogError(ex, "Error while consuming message from Kafka: {Reason}", ex.Error.Reason);
            return null;
        }
    }

    public void Commit(ConsumeResult<string, string> result)
    {
        try
        {
            _consumer.Commit(result);
            _logger.LogDebug("Committed offset {Offset} for partition {Partition}", result.Offset, result.Partition);
        }
        catch (KafkaException ex)
        {
            _logger.LogError(ex, "Failed to commit Kafka offset: {Reason}", ex.Error.Reason);
            throw;
        }
    }

    public async Task SendToDlqAsync(string key, string value, string reason, Headers? headers = null, CancellationToken ct = default)
    {
        if (_dlqProducer is null)
        {
            _logger.LogWarning("DLQ producer not configured, skipping DLQ dispatch for key {Key}", key);
            return;
        }

        var dlqHeaders = headers ?? new Headers();
        dlqHeaders.Add("dlq_reason", Encoding.UTF8.GetBytes(reason));
        dlqHeaders.Add("dlq_source_topic", Encoding.UTF8.GetBytes(_options.Topic));
        dlqHeaders.Add("dlq_timestamp", Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("O")));

        var message = new Message<string, string>
        {
            Key = key,
            Value = value,
            Headers = dlqHeaders
        };

        _logger.LogWarning("Sending poison message with key {Key} to DLQ topic {DlqTopic}. Reason: {Reason}", key, _options.DeadLetterQueueTopic, reason);
        await _dlqProducer.ProduceAsync(_options.DeadLetterQueueTopic, message, ct);
    }

    public void Close()
    {
        try
        {
            _consumer.Close();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Exception while closing Kafka consumer");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_ownsConsumer)
        {
            _consumer.Dispose();
        }

        if (_ownsProducer && _dlqProducer is not null)
        {
            _dlqProducer.Dispose();
        }
    }
}
