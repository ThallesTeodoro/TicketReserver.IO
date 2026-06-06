using Microsoft.Extensions.Logging;
using TR.HW.OutboxRelay.Application.Ports;

namespace TR.HW.OutboxRelay.Application.UseCases.RelayMessages;

public class RelayMessagesUseCase
{
    private readonly IKafkaPort _kafkaPort;
    private readonly IOutboxPort _outboxPort;
    private readonly ILogger<RelayMessagesUseCase> _logger;

    public RelayMessagesUseCase(
        IKafkaPort kafkaPort,
        IOutboxPort outboxPort,
        ILogger<RelayMessagesUseCase> logger)
    {
        _kafkaPort = kafkaPort;
        _outboxPort = outboxPort;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        var messages = await _outboxPort.GetPendingMessagesAsync(50);

        if (!messages.Any())
        {
            return;
        }

        _logger.LogInformation("Processing {Count} outbox messages.", messages.Count());

        foreach (var message in messages)
        {
            try
            {
                await _kafkaPort.PublishAsync(
                    topic: message.EventType,
                    key: message.Id.ToString(),
                    payload: message.Payload,
                    correlationId: message.CorrelationId
                );

                await _outboxPort.DeleteMessageAsync(message.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox message {Id}.", message.Id);
            }
        }
    }
}
