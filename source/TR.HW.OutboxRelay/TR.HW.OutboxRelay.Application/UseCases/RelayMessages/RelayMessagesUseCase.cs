using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TR.HW.OutboxRelay.Application.Common.Config;
using TR.HW.OutboxRelay.Application.Ports;
using TR.HW.OutboxRelay.Domain;

namespace TR.HW.OutboxRelay.Application.UseCases.RelayMessages;

public class RelayMessagesUseCase
{
    private readonly IKafkaPort _kafkaPort;
    private readonly IOutboxPort _outboxPort;
    private readonly ILogger<RelayMessagesUseCase> _logger;
    private readonly OutboxSettings _settings;

    public RelayMessagesUseCase(
        IKafkaPort kafkaPort,
        IOutboxPort outboxPort,
        ILogger<RelayMessagesUseCase> logger,
        IOptions<OutboxSettings> settings)
    {
        _kafkaPort = kafkaPort;
        _outboxPort = outboxPort;
        _logger = logger;
        _settings = settings.Value;
    }

    public async Task ExecuteAsync()
    {
        using var activity = Telemetry.ActivitySource.StartActivity("RelayMessagesUseCase.Execute");
        
        var timeoutLimit = DateTime.UtcNow.AddMinutes(-_settings.TimeoutMinutes);

        var messages = await _outboxPort.GetAndLockEligibleMessagesAsync(_settings.BatchSize, timeoutLimit);
        var messageList = messages.ToList();

        if (!messageList.Any())
        {
            return;
        }

        activity?.SetTag("messages.count", messageList.Count);
        _logger.LogInformation("Found and locked {Count} eligible outbox messages.", messageList.Count);

        foreach (var message in messageList)
        {
            using var messageActivity = Telemetry.ActivitySource.StartActivity("ProcessMessage");
            messageActivity?.SetTag("message.id", message.Id);
            messageActivity?.SetTag("message.event_name", message.EventName);

            try
            {
                await _kafkaPort.PublishAsync(
                    topic: message.EventType,
                    key: message.Id.ToString(),
                    payload: message.Payload,
                    correlationId: message.CorrelationId
                );

                await _outboxPort.MarkAsProcessedAsync(message.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox message {Id}.", message.Id);
                messageActivity?.SetStatus(ActivityStatusCode.Error, ex.Message);

                var isPermanent = message.Attempts + 1 >= _settings.MaxAttempts;
                await _outboxPort.MarkAsFailedAsync(message.Id, ex.Message, isPermanent);
            }
        }
    }
}
