using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TR.HW.OutboxRelay.Application.Ports;
using TR.HW.OutboxRelay.Domain;

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
        using var activity = Telemetry.ActivitySource.StartActivity("RelayMessagesUseCase.Execute");
        
        var messages = await _outboxPort.GetPendingMessagesAsync(50);
        var messageList = messages.ToList();

        if (!messageList.Any())
        {
            return;
        }

        activity?.SetTag("messages.count", messageList.Count);
        _logger.LogInformation("Processing {Count} outbox messages.", messageList.Count);

        foreach (var message in messageList)
        {
            using var messageActivity = Telemetry.ActivitySource.StartActivity("ProcessMessage");
            messageActivity?.SetTag("message.id", message.Id);
            messageActivity?.SetTag("message.event_type", message.EventType);
            messageActivity?.SetTag("message.correlation_id", message.CorrelationId);

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
                messageActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            }
        }
    }
}
