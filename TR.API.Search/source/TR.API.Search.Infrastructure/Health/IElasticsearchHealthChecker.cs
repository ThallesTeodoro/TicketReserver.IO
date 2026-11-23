namespace TR.API.Search.Infrastructure.Health;

public interface IElasticsearchHealthChecker
{
    Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default);
}
