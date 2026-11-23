using TR.HW.Repl.Domain.Entities;

namespace TR.HW.Repl.Infrastructure.Persistence;

public interface IElasticsearchEngine
{
    Task IndexDocumentAsync(string indexName, EventSearchDocument document, CancellationToken cancellationToken = default);
    Task<EventSearchDocument?> GetDocumentAsync(string indexName, Guid id, CancellationToken cancellationToken = default);
    Task UpdateDocumentPartialAsync(string indexName, Guid id, object partialDoc, CancellationToken cancellationToken = default);
    Task DeleteDocumentAsync(string indexName, Guid id, CancellationToken cancellationToken = default);
}
