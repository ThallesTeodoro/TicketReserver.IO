using Dapper;
using Npgsql;
using Polly;
using Polly.Registry;
using TR.HW.OutboxRelay.Application.Ports;
using TR.HW.OutboxRelay.Domain.Entities;

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

    public async Task<IEnumerable<OutboxMessage>> GetPendingMessagesAsync(int limit)
    {
        return await _resiliencePipeline.ExecuteAsync(async ct =>
        {
            await using var connection = await _dataSource.OpenConnectionAsync(ct);
            const string sql = "SELECT * FROM OutboxMessages ORDER BY CreatedAt ASC LIMIT @Limit";
            return await connection.QueryAsync<OutboxMessage>(sql, new { Limit = limit });
        });
    }

    public async Task DeleteMessageAsync(Guid id)
    {
        await _resiliencePipeline.ExecuteAsync(async ct =>
        {
            await using var connection = await _dataSource.OpenConnectionAsync(ct);
            const string sql = "DELETE FROM OutboxMessages WHERE Id = @Id";
            await connection.ExecuteAsync(sql, new { Id = id });
        });
    }
}
