using Dapper;
using Npgsql;
using Polly;
using Polly.Registry;
using TR.HW.OutboxRelay.Application.Ports;
using TR.HW.OutboxRelay.Domain.Entities;
using TR.HW.OutboxRelay.Domain.Enums;

namespace TR.HW.OutboxRelay.Infrastructure.Adapters.Data;

public class OutboxRepository : IOutboxPort
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ResiliencePipeline _resiliencePipeline;

    public OutboxRepository(NpgsqlDataSource dataSource, ResiliencePipelineProvider<string> pipelineProvider)
    {
        _dataSource = dataSource;
        _resiliencePipeline = pipelineProvider.GetPipeline("default");
    }

    public async Task<IEnumerable<OutboxMessage>> GetAndLockEligibleMessagesAsync(int limit, DateTime timeoutLimit)
    {
        return await _resiliencePipeline.ExecuteAsync(async ct =>
        {
            await using var connection = await _dataSource.OpenConnectionAsync(ct);
            
            // Query atômica: Bloqueia (FOR UPDATE SKIP LOCKED), atualiza para EmProcessamento e retorna os registros.
            const string sql = @"
                UPDATE OutboxMessages 
                SET Status = @EmProcessamento, LastAttemptAt = @Now 
                WHERE Id IN (
                    SELECT Id FROM OutboxMessages 
                    WHERE Status = @Pendente 
                       OR (Status = @EmProcessamento AND LastAttemptAt < @TimeoutLimit)
                    ORDER BY CreatedAt ASC 
                    LIMIT @Limit
                    FOR UPDATE SKIP LOCKED
                )
                RETURNING Id, CorrelationId, EventName, EventType, Payload, Status, Attempts, CreatedAt, LastAttemptAt, ErrorReason, ProcessedAt";

            return await connection.QueryAsync<OutboxMessage>(sql, new 
            { 
                EmProcessamento = (short)OutboxStatus.EmProcessamento,
                Pendente = (short)OutboxStatus.Pendente,
                Now = DateTime.UtcNow,
                TimeoutLimit = timeoutLimit,
                Limit = limit 
            });
        });
    }

    public async Task MarkAsProcessedAsync(Guid id)
    {
        await _resiliencePipeline.ExecuteAsync(async ct =>
        {
            await using var connection = await _dataSource.OpenConnectionAsync(ct);
            const string sql = @"
                UPDATE OutboxMessages 
                SET Status = @Processado, ProcessedAt = @Now 
                WHERE Id = @Id";

            await connection.ExecuteAsync(sql, new
            {
                Id = id,
                Processado = (short)OutboxStatus.Processado,
                Now = DateTime.UtcNow
            });
        });
    }

    public async Task MarkAsFailedAsync(Guid id, string errorReason, bool permanent)
    {
        await _resiliencePipeline.ExecuteAsync(async ct =>
        {
            await using var connection = await _dataSource.OpenConnectionAsync(ct);
            const string sql = @"
                UPDATE OutboxMessages 
                SET Status = @Status, 
                    Attempts = Attempts + 1, 
                    ErrorReason = @ErrorReason,
                    LastAttemptAt = @Now
                WHERE Id = @Id";

            var status = permanent ? OutboxStatus.ErroPermanente : OutboxStatus.Pendente;

            await connection.ExecuteAsync(sql, new
            {
                Id = id,
                Status = (short)status,
                ErrorReason = errorReason,
                Now = DateTime.UtcNow
            });
        });
    }
}
