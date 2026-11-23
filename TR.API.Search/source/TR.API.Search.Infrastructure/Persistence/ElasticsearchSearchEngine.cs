using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using TR.API.Search.Application.Features.Events.Models;
using TR.API.Search.Application.Features.Events.SearchEvents;
using TR.API.Search.Application.Interfaces;
using TR.API.Search.Domain.Entities;
using TR.API.Search.Domain.ValueObjects;
using TR.API.Search.Infrastructure.Configuration;
using TR.API.Search.Infrastructure.Models;

namespace TR.API.Search.Infrastructure.Persistence;

public class ElasticsearchSearchEngine : IEventSearchEngine
{
    private readonly IElasticsearchSearchReader _reader;
    private readonly ElasticsearchOptions _options;
    private readonly ILogger<ElasticsearchSearchEngine> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;

    public ElasticsearchSearchEngine(
        IElasticsearchSearchReader reader,
        IOptions<ElasticsearchOptions> options,
        ILogger<ElasticsearchSearchEngine> logger)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _retryPolicy = Policy
            .Handle<Exception>(ex => ex is not OperationCanceledException)
            .WaitAndRetryAsync(
                _options.RetryCount,
                retryAttempt => TimeSpan.FromMilliseconds(100 * Math.Pow(2, retryAttempt)),
                (exception, timeSpan, retryCount, _) =>
                {
                    _logger.LogWarning(
                        exception,
                        "ElasticSearch query attempt {RetryCount} failed. Retrying in {DelayMs}ms...",
                        retryCount,
                        timeSpan.TotalMilliseconds);
                });
    }

    public async Task<EventSearchResultDto> SearchEventsAsync(SearchEventsQuery query, CancellationToken ct = default)
    {
        var searchResult = await _retryPolicy.ExecuteAsync(() => _reader.SearchAsync(query, ct));

        var items = new List<EventSearchItemDto>();
        foreach (var hit in searchResult.Hits)
        {
            var doc = hit.Source;
            var lat = doc.Venue?.Location?.Lat ?? 0;
            var lon = doc.Venue?.Location?.Lon ?? 0;

            var tiers = doc.TiersSummary?
                .Select(t => new TicketTierSummaryDto(
                    Guid.TryParse(t.TierId, out var tid) ? tid : Guid.Empty,
                    t.Name,
                    t.Price,
                    t.Status))
                .ToList() ?? [];

            items.Add(new EventSearchItemDto(
                Guid.TryParse(doc.Id, out var id) ? id : Guid.Empty,
                doc.Title,
                doc.Slug,
                doc.Description,
                doc.Category,
                doc.Status,
                doc.AgeRestriction,
                doc.BannerUrl,
                doc.Schedule?.SalesStartAt ?? DateTimeOffset.UtcNow,
                doc.Schedule?.SalesEndAt ?? DateTimeOffset.UtcNow,
                doc.Schedule?.EventStartAt ?? DateTimeOffset.UtcNow,
                doc.Schedule?.EventEndAt ?? DateTimeOffset.UtcNow,
                Guid.TryParse(doc.Venue?.Id, out var vid) ? vid : Guid.Empty,
                doc.Venue?.Name ?? string.Empty,
                doc.Venue?.City ?? string.Empty,
                doc.Venue?.State ?? string.Empty,
                doc.Venue?.Country ?? string.Empty,
                lat,
                lon,
                doc.Pricing?.MinPrice ?? 0,
                doc.Pricing?.MaxPrice ?? 0,
                doc.Tags ?? [],
                tiers,
                hit.Score,
                hit.DistanceKm));
        }

        var categoryFacets = searchResult.CategoryFacets
            .Select(kv => new FacetCountDto(kv.Key, kv.Value))
            .ToList();

        var cityFacets = searchResult.CityFacets
            .Select(kv => new FacetCountDto(kv.Key, kv.Value))
            .ToList();

        return new EventSearchResultDto(
            items,
            searchResult.TotalCount,
            query.Page,
            query.PageSize,
            new SearchFacetsDto(categoryFacets, cityFacets));
    }

    public async Task<IReadOnlyCollection<AutocompleteSuggestionDto>> AutocompleteEventsAsync(string? prefix, int limit, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return Array.Empty<AutocompleteSuggestionDto>();
        }

        var results = await _retryPolicy.ExecuteAsync(() => _reader.AutocompleteAsync(prefix, limit, ct));

        return results
            .Select(r => new AutocompleteSuggestionDto(
                Guid.TryParse(r.Id, out var id) ? id : Guid.Empty,
                r.Title,
                r.Category))
            .ToList();
    }

    public async Task<EventSearchDocument?> GetEventByIdAsync(Guid eventId, CancellationToken ct = default)
    {
        var doc = await _retryPolicy.ExecuteAsync(() => _reader.GetByIdAsync(eventId, ct));
        if (doc == null)
        {
            return null;
        }

        var lat = doc.Venue?.Location?.Lat ?? 0;
        var lon = doc.Venue?.Location?.Lon ?? 0;

        var tiers = doc.TiersSummary?
            .Select(t => new TicketTierSummary
            {
                TierId = Guid.TryParse(t.TierId, out var tid) ? tid : Guid.Empty,
                TierName = t.Name,
                Price = t.Price,
                Status = t.Status
            })
            .ToList() ?? [];

        return new EventSearchDocument
        {
            Id = Guid.TryParse(doc.Id, out var id) ? id : Guid.Empty,
            Title = doc.Title,
            Slug = doc.Slug,
            Description = doc.Description,
            Category = doc.Category,
            Status = doc.Status,
            AgeRestriction = doc.AgeRestriction,
            BannerUrl = doc.BannerUrl,
            SalesStartAt = doc.Schedule?.SalesStartAt ?? DateTimeOffset.UtcNow,
            SalesEndAt = doc.Schedule?.SalesEndAt ?? DateTimeOffset.UtcNow,
            EventStartAt = doc.Schedule?.EventStartAt ?? DateTimeOffset.UtcNow,
            EventEndAt = doc.Schedule?.EventEndAt ?? DateTimeOffset.UtcNow,
            VenueId = Guid.TryParse(doc.Venue?.Id, out var vid) ? vid : Guid.Empty,
            VenueName = doc.Venue?.Name ?? string.Empty,
            VenueCity = doc.Venue?.City ?? string.Empty,
            VenueState = doc.Venue?.State ?? string.Empty,
            VenueCountry = doc.Venue?.Country ?? string.Empty,
            VenueLocation = new GeoLocation(lat, lon),
            MinPrice = doc.Pricing?.MinPrice ?? 0,
            MaxPrice = doc.Pricing?.MaxPrice ?? 0,
            Tags = doc.Tags ?? [],
            Tiers = tiers
        };
    }
}
