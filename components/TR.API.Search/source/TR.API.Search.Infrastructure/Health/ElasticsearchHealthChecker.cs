using Microsoft.Extensions.Logging;
using TR.API.Search.Infrastructure.Persistence;

namespace TR.API.Search.Infrastructure.Health;

public class ElasticsearchHealthChecker : IElasticsearchHealthChecker
{
    private readonly IElasticsearchSearchReader _reader;
    private readonly ILogger<ElasticsearchHealthChecker> _logger;

    public ElasticsearchHealthChecker(
        IElasticsearchSearchReader reader,
        ILogger<ElasticsearchHealthChecker> logger)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var isHealthy = await _reader.PingAsync(cancellationToken);
            if (!isHealthy)
            {
                _logger.LogWarning("ElasticSearch ping returned unhealth status");
            }
            return isHealthy;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred during ElasticSearch health check");
            return false;
        }
    }
}
