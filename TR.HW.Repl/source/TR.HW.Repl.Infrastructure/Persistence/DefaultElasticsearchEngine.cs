using Elastic.Clients.Elasticsearch;
using Microsoft.Extensions.Logging;
using TR.HW.Repl.Domain.Entities;

namespace TR.HW.Repl.Infrastructure.Persistence;

public sealed class DefaultElasticsearchEngine : IElasticsearchEngine
{
    private readonly ElasticsearchClient _client;
    private readonly ILogger<DefaultElasticsearchEngine> _logger;

    public DefaultElasticsearchEngine(ElasticsearchClient client, ILogger<DefaultElasticsearchEngine> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task IndexDocumentAsync(string indexName, EventSearchDocument document, CancellationToken cancellationToken = default)
    {
        var response = await _client.IndexAsync(document, idx => idx
            .Index(indexName)
            .Id(document.Id.ToString()), cancellationToken);

        if (!response.IsValidResponse)
        {
            _logger.LogError("ElasticSearch indexing error: {Error}", response.DebugInformation);
            throw new InvalidOperationException($"Failed to index document {document.Id} into {indexName}: {response.ElasticsearchServerError?.Error.Reason}");
        }
    }

    public async Task<EventSearchDocument?> GetDocumentAsync(string indexName, Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _client.GetAsync<EventSearchDocument>(id.ToString(), g => g.Index(indexName), cancellationToken);
        if (!response.IsValidResponse || !response.Found)
        {
            return null;
        }

        return response.Source;
    }

    public async Task UpdateDocumentPartialAsync(string indexName, Guid id, object partialDoc, CancellationToken cancellationToken = default)
    {
        var response = await _client.UpdateAsync<EventSearchDocument, object>(indexName, id.ToString(), u => u
            .Doc(partialDoc)
            .DocAsUpsert(false), cancellationToken);

        if (!response.IsValidResponse)
        {
            _logger.LogError("ElasticSearch partial update error: {Error}", response.DebugInformation);
            throw new InvalidOperationException($"Failed to update document {id} in {indexName}: {response.ElasticsearchServerError?.Error.Reason}");
        }
    }

    public async Task DeleteDocumentAsync(string indexName, Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _client.DeleteAsync<EventSearchDocument>(id.ToString(), d => d.Index(indexName), cancellationToken);
        if (!response.IsValidResponse && response.ElasticsearchServerError?.Status != 404)
        {
            _logger.LogError("ElasticSearch delete error: {Error}", response.DebugInformation);
            throw new InvalidOperationException($"Failed to delete document {id} from {indexName}: {response.ElasticsearchServerError?.Error.Reason}");
        }
    }
}
