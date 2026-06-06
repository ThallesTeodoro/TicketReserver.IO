using System.ComponentModel;
using System.Diagnostics;
using Hangfire;
using TR.HW.OutboxRelay.Application.UseCases.RelayMessages;

namespace TR.HW.OutboxRelay.Worker.Jobs;

public class OutboxRelayJob
{
    private readonly RelayMessagesUseCase _useCase;
    private readonly ILogger<OutboxRelayJob> _logger;

    public OutboxRelayJob(RelayMessagesUseCase useCase, ILogger<OutboxRelayJob> logger)
    {
        _useCase = useCase;
        _logger = logger;
    }

    [DisplayName("Outbox Message Relay")]
    [AutomaticRetry(Attempts = 3)]
    public async Task ExecuteAsync()
    {
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("Outbox Relay Job started at: {Time}", DateTimeOffset.Now);
        
        try
        {
            await _useCase.ExecuteAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error during Outbox Relay Job execution.");
            throw;
        }
        finally
        {
            sw.Stop();
            _logger.LogInformation("Outbox Relay Job finished at: {Time}. Duration: {Duration}ms", 
                DateTimeOffset.Now, sw.ElapsedMilliseconds);
        }
    }
}
