using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Aggregations;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TR.API.Search.Application.Features.Events.SearchEvents;
using TR.API.Search.Domain.ValueObjects;
using TR.API.Search.Infrastructure.Configuration;
using TR.API.Search.Infrastructure.Models;
using DomainGeoLocation = TR.API.Search.Domain.ValueObjects.GeoLocation;
using EsGeoLoc = Elastic.Clients.Elasticsearch.GeoLocation;

namespace TR.API.Search.Infrastructure.Persistence;

public class DefaultElasticsearchSearchReader : IElasticsearchSearchReader
{
    private readonly ElasticsearchClient _client;
    private readonly ElasticsearchOptions _options;
    private readonly ILogger<DefaultElasticsearchSearchReader> _logger;

    public DefaultElasticsearchSearchReader(
        ElasticsearchClient client,
        IOptions<ElasticsearchOptions> options,
        ILogger<DefaultElasticsearchSearchReader> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<EsSearchResult<EsEventDocument>> SearchAsync(SearchEventsQuery query, CancellationToken ct = default)
    {
        var from = (query.Page - 1) * query.PageSize;
        var size = query.PageSize;

        var searchResponse = await _client.SearchAsync<EsEventDocument>(s => s
            .Index(_options.IndexName)
            .From(from)
            .Size(size)
            .Query(q => BuildBoolQuery(q, query))
            .Sort(sort => ApplySorting(sort, query))
            .Aggregations(a => a
                .Add("categories", agg => agg.Terms(t => t.Field("category").Size(20)))
                .Add("cities", agg => agg.Terms(t => t.Field("venue.city").Size(20)))
            ), ct);

        if (!searchResponse.IsValidResponse)
        {
            _logger.LogError("ElasticSearch search failed: {Error}", searchResponse.DebugInformation);
            throw new InvalidOperationException($"ElasticSearch query failed: {searchResponse.ElasticsearchServerError?.Error.Reason ?? searchResponse.DebugInformation}");
        }

        var hits = new List<EsSearchHit<EsEventDocument>>();
        foreach (var hit in searchResponse.Hits)
        {
            if (hit.Source == null) continue;

            double? distance = null;
            if (query.Latitude.HasValue && query.Longitude.HasValue && hit.Source.Venue?.Location != null)
            {
                var refGeo = new DomainGeoLocation(query.Latitude.Value, query.Longitude.Value);
                var venueGeo = new DomainGeoLocation(hit.Source.Venue.Location.Lat, hit.Source.Venue.Location.Lon);
                distance = Math.Round(refGeo.DistanceToKm(venueGeo), 2);
            }

            hits.Add(new EsSearchHit<EsEventDocument>(
                hit.Source,
                hit.Score,
                distance));
        }

        var categoryFacets = new Dictionary<string, long>();
        if (searchResponse.Aggregations != null && searchResponse.Aggregations.TryGetValue("categories", out var catAgg) && catAgg is StringTermsAggregate catTerms)
        {
            foreach (var bucket in catTerms.Buckets)
            {
                var key = bucket.Key.ToString();
                if (!string.IsNullOrEmpty(key))
                {
                    categoryFacets[key] = bucket.DocCount;
                }
            }
        }

        var cityFacets = new Dictionary<string, long>();
        if (searchResponse.Aggregations != null && searchResponse.Aggregations.TryGetValue("cities", out var cityAgg) && cityAgg is StringTermsAggregate cityTerms)
        {
            foreach (var bucket in cityTerms.Buckets)
            {
                var key = bucket.Key.ToString();
                if (!string.IsNullOrEmpty(key))
                {
                    cityFacets[key] = bucket.DocCount;
                }
            }
        }

        var total = searchResponse.Total;

        return new EsSearchResult<EsEventDocument>(
            hits,
            total,
            categoryFacets,
            cityFacets);
    }

    public async Task<IReadOnlyCollection<EsAutocompleteItem>> AutocompleteAsync(string prefix, int limit, CancellationToken ct = default)
    {
        var response = await _client.SearchAsync<EsEventDocument>(s => s
            .Index(_options.IndexName)
            .From(0)
            .Size(limit)
            .Query(q => q
                .Match(m => m
                    .Field("title.autocomplete")
                    .Query(prefix)
                )
            ), ct);

        if (!response.IsValidResponse)
        {
            _logger.LogError("ElasticSearch autocomplete failed: {Error}", response.DebugInformation);
            return Array.Empty<EsAutocompleteItem>();
        }

        var results = new List<EsAutocompleteItem>();
        foreach (var hit in response.Hits)
        {
            if (hit.Source != null)
            {
                results.Add(new EsAutocompleteItem(
                    hit.Source.Id,
                    hit.Source.Title,
                    hit.Source.Category));
            }
        }

        return results;
    }

    public async Task<EsEventDocument?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _client.GetAsync<EsEventDocument>(id.ToString(), g => g.Index(_options.IndexName), ct);
        if (!response.IsValidResponse || !response.Found)
        {
            return null;
        }

        return response.Source;
    }

    public async Task<bool> PingAsync(CancellationToken ct = default)
    {
        try
        {
            var ping = await _client.PingAsync(ct);
            return ping.IsValidResponse;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ElasticSearch ping check failed");
            return false;
        }
    }

    private static void BuildBoolQuery(QueryDescriptor<EsEventDocument> q, SearchEventsQuery query)
    {
        q.Bool(b =>
        {
            if (!string.IsNullOrWhiteSpace(query.Query))
            {
                b.Must(must => must
                    .MultiMatch(mm => mm
                        .Query(query.Query)
                        .Fields(new[] { "title^3", "category^2", "tags^1.5", "description" })
                        .Fuzziness(new Fuzziness("AUTO"))
                    )
                );
            }

            var filterList = new List<Action<QueryDescriptor<EsEventDocument>>>();

            if (!string.IsNullOrWhiteSpace(query.Status))
            {
                filterList.Add(f => f.Term(t => t.Field("status").Value(query.Status.ToUpperInvariant())));
            }

            if (!string.IsNullOrWhiteSpace(query.Category))
            {
                filterList.Add(f => f.Term(t => t.Field("category").Value(query.Category.ToUpperInvariant())));
            }

            if (query.MinPrice.HasValue || query.MaxPrice.HasValue)
            {
                filterList.Add(f => f.Range(r => r.NumberRange(nr =>
                {
                    nr.Field("pricing.min_price");
                    if (query.MinPrice.HasValue) nr.Gte((double)query.MinPrice.Value);
                    if (query.MaxPrice.HasValue) nr.Lte((double)query.MaxPrice.Value);
                })));
            }

            if (query.FromDate.HasValue || query.ToDate.HasValue)
            {
                filterList.Add(f => f.Range(r => r.DateRange(dr =>
                {
                    dr.Field("schedule.event_start_at");
                    if (query.FromDate.HasValue) dr.Gte(query.FromDate.Value.ToString("O"));
                    if (query.ToDate.HasValue) dr.Lte(query.ToDate.Value.ToString("O"));
                })));
            }

            if (query.Latitude.HasValue && query.Longitude.HasValue && query.RadiusKm.HasValue)
            {
                filterList.Add(f => f.GeoDistance(gd => gd
                    .Field("venue.location")
                    .Distance($"{query.RadiusKm.Value}km")
                    .Location(new LatLonGeoLocation { Lat = query.Latitude.Value, Lon = query.Longitude.Value })
                ));
            }

            if (filterList.Count > 0)
            {
                b.Filter(filterList.ToArray());
            }
        });
    }

    private static void ApplySorting(SortOptionsDescriptor<EsEventDocument> sort, SearchEventsQuery query)
    {
        switch (query.SortBy)
        {
            case SearchSortOption.DateAsc:
                sort.Field("schedule.event_start_at", s => s.Order(SortOrder.Asc));
                break;
            case SearchSortOption.DateDesc:
                sort.Field("schedule.event_start_at", s => s.Order(SortOrder.Desc));
                break;
            case SearchSortOption.PriceAsc:
                sort.Field("pricing.min_price", s => s.Order(SortOrder.Asc));
                break;
            case SearchSortOption.PriceDesc:
                sort.Field("pricing.max_price", s => s.Order(SortOrder.Desc));
                break;
            case SearchSortOption.Distance:
                if (query.Latitude.HasValue && query.Longitude.HasValue)
                {
                    sort.GeoDistance(g => g
                        .Field("venue.location")
                        .Location(new Elastic.Clients.Elasticsearch.GeoLocation[]
                        {
                            new LatLonGeoLocation { Lat = query.Latitude.Value, Lon = query.Longitude.Value }
                        })
                        .Order(SortOrder.Asc)
                        .Unit(DistanceUnit.Kilometers)
                    );
                }
                break;
            case SearchSortOption.Relevance:
            default:
                if (!string.IsNullOrWhiteSpace(query.Query))
                {
                    sort.Score(s => s.Order(SortOrder.Desc));
                }
                else
                {
                    sort.Field("schedule.event_start_at", s => s.Order(SortOrder.Asc));
                }
                break;
        }
    }
}
