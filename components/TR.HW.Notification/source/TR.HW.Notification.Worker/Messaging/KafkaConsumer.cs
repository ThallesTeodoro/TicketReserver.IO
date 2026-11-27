using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TR.HW.Notification.Worker.Configuration;

namespace TR.HW.Notification.Worker.Messaging;

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
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

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
        _logger.LogInformation(
            "Subscribing to topics [{Topics}] with group {GroupId}",
            string.Join(", ", _options.Topics),
            _options.GroupId);

        _consumer.Subscribe(_options.Topics);
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

    public async Task SendToDlqAsync(
        string topic,
        string key,
        string value,
        string reason,
        Headers? headers = null,
        CancellationToken ct = default)
    {
        if (_dlqProducer is null)
        {
            _logger.LogWarning("DLQ Producer is null; message cannot be sent to DLQ");
            return;
        }

        var dlqTopic = topic.EndsWith(".DLQ", StringComparison.OrdinalIgnoreCase) ? topic : $"{topic}.DLQ";

        try
        {
            var dlqHeaders = new Headers();
            if (headers is not null)
            {
                foreach (var header in headers)
                {
                    dlqHeaders.Add(header.Key, header.GetValueBytes());
                }
            }

            dlqHeaders.Add("dlq-reason", Encoding.UTF8.GetBytes(reason));
            dlqHeaders.Add("dlq-timestamp", Encoding.UTF8.GetBytes(DateTimeOffset.UtcNow.ToString("O")));

            var message = new Message<string, string>
            {
                Key = key,
                Value = value,
                Headers = dlqHeaders,
                Timestamp = new Timestamp(DateTime.UtcNow)
            };

            await _dlqProducer.ProduceAsync(dlqTopic, message, ct);
            _logger.LogWarning("Sent poisoned message to DLQ topic {DlqTopic} for key {Key}", dlqTopic, key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message to DLQ topic {DlqTopic}", dlqTopic);
        }
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

        Close();

        if (_ownsConsumer)
        {
            _consumer.Dispose();
        }

        if (_ownsProducer && _dlqProducer is not null)
        {
            _dlqProducer.Flush(TimeSpan.FromSeconds(5));
            _dlqProducer.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}
