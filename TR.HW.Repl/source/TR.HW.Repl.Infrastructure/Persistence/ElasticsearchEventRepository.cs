using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using TR.HW.Repl.Application.Interfaces;
using TR.HW.Repl.Domain.Entities;
using TR.HW.Repl.Infrastructure.Configuration;

namespace TR.HW.Repl.Infrastructure.Persistence;

public sealed class ElasticsearchEventRepository : IEventSearchRepository
{
    private readonly IElasticsearchEngine _engine;
    private readonly ElasticsearchOptions _options;
    private readonly ILogger<ElasticsearchEventRepository> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;

    public ElasticsearchEventRepository(
        IElasticsearchEngine engine,
        IOptions<ElasticsearchOptions> options,
        ILogger<ElasticsearchEventRepository> logger)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _retryPolicy = Policy
            .Handle<Exception>(ex => ex is not ArgumentException)
            .WaitAndRetryAsync(
                _options.RetryCount,
                attempt => TimeSpan.FromMilliseconds(50 * Math.Pow(2, attempt)) + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 50)),
                (outcome, timespan, retryAttempt, context) =>
                {
                    _logger.LogWarning(
                        "ElasticSearch operation retry {Attempt} after {Delay}ms due to: {Message}",
                        retryAttempt,
                        timespan.TotalMilliseconds,
                        outcome.Message);
                });
    }

    public Task UpsertEventAsync(EventSearchDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        return _retryPolicy.ExecuteAsync(() =>
            _engine.IndexDocumentAsync(_options.IndexName, document, cancellationToken));
    }

    public Task UpdateEventStatusAsync(Guid eventId, string status, DateTimeOffset updatedAt, CancellationToken cancellationToken = default)
    {
        if (eventId == Guid.Empty)
            throw new ArgumentException("EventId cannot be empty.", nameof(eventId));

        return _retryPolicy.ExecuteAsync(() =>
            _engine.UpdateDocumentPartialAsync(
                _options.IndexName,
                eventId,
                new { status = status.ToUpperInvariant(), updated_at = updatedAt },
                cancellationToken));
    }

    public Task DeleteEventAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        if (eventId == Guid.Empty)
            throw new ArgumentException("EventId cannot be empty.", nameof(eventId));

        return _retryPolicy.ExecuteAsync(() =>
            _engine.DeleteDocumentAsync(_options.IndexName, eventId, cancellationToken));
    }

    public Task<EventSearchDocument?> GetEventByIdAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        if (eventId == Guid.Empty)
            throw new ArgumentException("EventId cannot be empty.", nameof(eventId));

        return _retryPolicy.ExecuteAsync(() =>
            _engine.GetDocumentAsync(_options.IndexName, eventId, cancellationToken));
    }

    public async Task UpdateTierPricingAsync(Guid eventId, TicketTierSummary tier, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tier);
        if (eventId == Guid.Empty)
            throw new ArgumentException("EventId cannot be empty.", nameof(eventId));

        var doc = await GetEventByIdAsync(eventId, cancellationToken);
        if (doc == null)
        {
            _logger.LogWarning("Cannot update tier for event {EventId} because event document does not exist yet", eventId);
            return;
        }

        doc.UpsertTier(tier);
        await UpsertEventAsync(doc, cancellationToken);
    }

    public async Task RemoveTierPricingAsync(Guid eventId, Guid tierId, CancellationToken cancellationToken = default)
    {
        if (eventId == Guid.Empty)
            throw new ArgumentException("EventId cannot be empty.", nameof(eventId));
        if (tierId == Guid.Empty)
            throw new ArgumentException("TierId cannot be empty.", nameof(tierId));

        var doc = await GetEventByIdAsync(eventId, cancellationToken);
        if (doc == null)
        {
            _logger.LogWarning("Cannot remove tier {TierId} from event {EventId} because event does not exist", tierId, eventId);
            return;
        }

        doc.RemoveTier(tierId);
        await UpsertEventAsync(doc, cancellationToken);
    }

    public async Task UpdateVenueInfoAsync(
        Guid venueId,
        string name,
        string city,
        string state,
        string country,
        double lat,
        double lon,
        CancellationToken cancellationToken = default)
    {
        if (venueId == Guid.Empty)
            throw new ArgumentException("VenueId cannot be empty.", nameof(venueId));

        // Part of venue CDC sync: in search documents, venue is embedded inside event documents
        await Task.CompletedTask;
    }
}
