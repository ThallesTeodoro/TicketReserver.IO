namespace TR.HW.ReservationOutboxRelay.Workers;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TR.HW.ReservationOutboxRelay.Configuration;
using TR.HW.ReservationOutboxRelay.Messaging;
using TR.HW.ReservationOutboxRelay.Persistence;

public class ReservationOutboxRelayWorker(
    IServiceScopeFactory scopeFactory,
    IKafkaProducer kafkaProducer,
    IOptions<OutboxRelayOptions> options,
    ILogger<ReservationOutboxRelayWorker> logger) : BackgroundService
{
    private readonly OutboxRelayOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Iniciando ReservationOutboxRelayWorker (BatchSize: {BatchSize}, Interval: {Interval}ms)...",
            _options.BatchSize, _options.IntervalMilliseconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            var processedCount = 0;
            try
            {
                processedCount = await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro inesperado no loop principal de despacho do outbox.");
            }

            var delay = processedCount > 0 ? _options.IntervalMilliseconds : _options.EmptyDelayMilliseconds;
            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        logger.LogInformation("ReservationOutboxRelayWorker finalizado.");
    }

    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();

        var messages = await outboxRepository.GetPendingBatchAsync(_options.BatchSize, cancellationToken);
        TicketReserver.Diagnostics.TicketReserverDiagnostics.UpdateOutboxPendingMessages(messages.Count);
        if (!messages.Any())
        {
            return 0;
        }

        logger.LogDebug("Encontradas {Count} mensagens pendentes no outbox para despacho.", messages.Count);

        foreach (var message in messages)
        {
            await ProcessSingleMessageAsync(outboxRepository, message, cancellationToken);
        }

        return messages.Count;
    }

    public async Task ProcessSingleMessageAsync(
        IOutboxRepository repository,
        OutboxMessageRecord message,
        CancellationToken cancellationToken)
    {
        try
        {
            await kafkaProducer.ProduceAsync(message, cancellationToken);
            await repository.MarkAsPublishedAsync(message.Id, DateTime.UtcNow, cancellationToken);
            TicketReserver.Diagnostics.TicketReserverDiagnostics.RecordSagaEvent(kafkaProducer.ResolveTopic(message.EventType), "dispatched");
            logger.LogInformation("Mensagem {MessageId} publicada no Kafka e marcada como PUBLISHED.", message.Id);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao despachar mensagem {MessageId}. Tentativa atual: {RetryCount}",
                message.Id, message.RetryCount);

            if (message.RetryCount + 1 >= _options.MaxRetries)
            {
                logger.LogError(
                    "Mensagem {MessageId} excedeu o limite de {MaxRetries} retentativas. Encaminhando para DLQ e marcando como FAILED.",
                    message.Id, _options.MaxRetries);

                try
                {
                    await kafkaProducer.ProduceToDeadLetterQueueAsync(message, ex.Message, cancellationToken);
                }
                catch (Exception dlqEx)
                {
                    logger.LogCritical(dlqEx, "Falha ao encaminhar mensagem {MessageId} para a DLQ.", message.Id);
                }

                await repository.MarkAsFailedAsync(message.Id, ex.Message, DateTime.UtcNow, cancellationToken);
            }
            else
            {
                var nextDelaySeconds = (message.RetryCount + 1) * _options.RetryBaseDelaySeconds;
                await repository.IncrementRetryAsync(message.Id, ex.Message, nextDelaySeconds, cancellationToken);
            }
        }
    }
}
