using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using TR.HW.Repl.Application.Common.Messaging;
using TR.HW.Repl.Application.Features.Replication;
using TR.HW.Repl.Worker.Messaging;

namespace TR.HW.Repl.Worker.Workers;

public class SearchReplicationWorker : BackgroundService
{
    private readonly IKafkaConsumer _kafkaConsumer;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SearchReplicationWorker> _logger;

    public SearchReplicationWorker(
        IKafkaConsumer kafkaConsumer,
        IServiceScopeFactory scopeFactory,
        ILogger<SearchReplicationWorker> logger)
    {
        _kafkaConsumer = kafkaConsumer ?? throw new ArgumentNullException(nameof(kafkaConsumer));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting SearchReplicationWorker...");
        _kafkaConsumer.Subscribe();

        while (!stoppingToken.IsCancellationRequested)
        {
            ConsumeResult<string, string>? consumeResult = null;

            try
            {
                consumeResult = _kafkaConsumer.Consume(TimeSpan.FromMilliseconds(500));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error consuming CDC message");
                await Task.Delay(1000, stoppingToken);
                continue;
            }

            if (consumeResult == null || consumeResult.Message == null)
            {
                continue;
            }

            var message = consumeResult.Message;
            if (string.IsNullOrWhiteSpace(message.Value))
            {
                // Tombstone or empty record
                _logger.LogDebug("Received empty or tombstone record on topic {Topic}", consumeResult.Topic);
                _kafkaConsumer.Commit(consumeResult);
                continue;
            }

            try
            {
                await ProcessMessageAsync(consumeResult.Topic, message.Key, message.Value, stoppingToken);
                _kafkaConsumer.Commit(consumeResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to process CDC record on {Topic} partition {Partition} offset {Offset}. Routing to DLQ.",
                    consumeResult.Topic,
                    consumeResult.Partition,
                    consumeResult.Offset);

                try
                {
                    await _kafkaConsumer.SendToDlqAsync(
                        consumeResult.Topic,
                        message.Key,
                        message.Value,
                        ex.Message,
                        message.Headers,
                        stoppingToken);

                    _kafkaConsumer.Commit(consumeResult);
                }
                catch (Exception dlqEx)
                {
                    _logger.LogCritical(
                        dlqEx,
                        "CRITICAL: Failed to produce CDC poison pill to DLQ on {Topic} offset {Offset}",
                        consumeResult.Topic,
                        consumeResult.Offset);
                }
            }
        }

        _logger.LogInformation("SearchReplicationWorker stopping, closing Kafka consumer...");
        _kafkaConsumer.Close();
    }

    private async Task ProcessMessageAsync(string topic, string? key, string value, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();

        if (topic.EndsWith(".events", StringComparison.OrdinalIgnoreCase))
        {
            var command = CdcMessageParser.ParseEventCommand(value, key);
            var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<ReplicateEventChangeCommand, ReplicationResult>>();
            var result = await handler.HandleAsync(command, ct);

            if (!result.Success)
            {
                throw new InvalidOperationException($"Replication command for event {command.EventId} failed: {result.ErrorMessage}");
            }
        }
        else if (topic.EndsWith(".ticket_tiers", StringComparison.OrdinalIgnoreCase))
        {
            var command = CdcMessageParser.ParseTicketTierCommand(value, key);
            var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<ReplicateTicketTierChangeCommand, ReplicationResult>>();
            var result = await handler.HandleAsync(command, ct);

            if (!result.Success)
            {
                throw new InvalidOperationException($"Replication command for tier {command.TierId} on event {command.EventId} failed: {result.ErrorMessage}");
            }
        }
        else if (topic.EndsWith(".venues", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Received venue CDC update on {Topic}. Venues are currently denormalized via event updates.", topic);
        }
        else
        {
            _logger.LogWarning("Unknown topic {Topic} received in SearchReplicationWorker", topic);
        }
    }
}
