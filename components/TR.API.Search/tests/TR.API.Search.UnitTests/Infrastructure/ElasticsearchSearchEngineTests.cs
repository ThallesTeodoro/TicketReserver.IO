using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TR.API.Search.Application.Features.Events.SearchEvents;
using TR.API.Search.Domain.ValueObjects;
using TR.API.Search.Infrastructure.Configuration;
using TR.API.Search.Infrastructure.Health;
using TR.API.Search.Infrastructure.Models;
using TR.API.Search.Infrastructure.Persistence;
using Xunit;

namespace TR.API.Search.UnitTests.Infrastructure;

public class ElasticsearchSearchEngineTests
{
    private readonly Mock<IElasticsearchSearchReader> _readerMock = new();
    private readonly Mock<ILogger<ElasticsearchSearchEngine>> _loggerMock = new();
    private readonly Mock<ILogger<ElasticsearchHealthChecker>> _healthLoggerMock = new();
    private readonly IOptions<ElasticsearchOptions> _options = Options.Create(new ElasticsearchOptions
    {
        Uri = "http://localhost:9200",
        IndexName = "events_search",
        RetryCount = 2
    });

    [Fact]
    public void ElasticsearchOptions_Validate_WhenInvalid_ShouldThrow()
    {
        var opt = new ElasticsearchOptions { Uri = "" };
        var act = () => opt.Validate();
        act.Should().Throw<ArgumentNullException>().WithParameterName("Uri");

        var opt2 = new ElasticsearchOptions { Uri = "http://es", IndexName = "" };
        var act2 = () => opt2.Validate();
        act2.Should().Throw<ArgumentNullException>().WithParameterName("IndexName");
    }

    [Fact]
    public async Task SearchEventsAsync_ShouldMapHitsAndFacetsCorrectly()
    {
        var eventId = Guid.NewGuid();
        var venueId = Guid.NewGuid();
        var query = new SearchEventsQuery("rock", Latitude: -23.55, Longitude: -46.63);

        var hit = new EsSearchHit<EsEventDocument>(
            new EsEventDocument
            {
                Id = eventId.ToString(),
                Title = "Rock Fest",
                Slug = "rock-fest",
                Category = "MUSIC",
                Status = "PUBLISHED",
                Venue = new EsVenue
                {
                    Id = venueId.ToString(),
                    Name = "Allianz",
                    City = "Sao Paulo",
                    Location = new EsGeoLocation { Lat = -23.52, Lon = -46.67 }
                },
                Pricing = new EsPricing { MinPrice = 120, MaxPrice = 300 },
                TiersSummary = new List<EsTicketTier>
                {
                    new() { TierId = Guid.NewGuid().ToString(), Name = "Pista", Price = 120, Status = "AVAILABLE" }
                }
            },
            Score: 2.5,
            DistanceKm: 5.2);

        var readerResult = new EsSearchResult<EsEventDocument>(
            new[] { hit },
            TotalCount: 1,
            CategoryFacets: new Dictionary<string, long> { ["MUSIC"] = 1 },
            CityFacets: new Dictionary<string, long> { ["Sao Paulo"] = 1 });

        _readerMock.Setup(x => x.SearchAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(readerResult);

        var engine = new ElasticsearchSearchEngine(_readerMock.Object, _options, _loggerMock.Object);

        var result = await engine.SearchEventsAsync(query);

        result.TotalCount.Should().Be(1);
        result.Items.Should().HaveCount(1);
        var item = result.Items.First();
        item.Id.Should().Be(eventId);
        item.Title.Should().Be("Rock Fest");
        item.RelevanceScore.Should().Be(2.5);
        item.DistanceKm.Should().Be(5.2);
        item.Tiers.Should().HaveCount(1);
        result.Facets.Categories.Should().Contain(c => c.Key == "MUSIC" && c.Count == 1);
        result.Facets.Cities.Should().Contain(c => c.Key == "Sao Paulo" && c.Count == 1);
    }

    [Fact]
    public async Task SearchEventsAsync_WhenTransientFailure_ShouldRetryAndSucceed()
    {
        var query = new SearchEventsQuery("test");
        var readerResult = new EsSearchResult<EsEventDocument>(
            Array.Empty<EsSearchHit<EsEventDocument>>(),
            0,
            new Dictionary<string, long>(),
            new Dictionary<string, long>());

        var attempts = 0;
        _readerMock.Setup(x => x.SearchAsync(query, It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                if (attempts++ == 0)
                {
                    throw new HttpRequestException("Transient network glitch");
                }
                return Task.FromResult(readerResult);
            });

        var engine = new ElasticsearchSearchEngine(_readerMock.Object, _options, _loggerMock.Object);

        var result = await engine.SearchEventsAsync(query);

        result.Should().NotBeNull();
        attempts.Should().Be(2);
    }

    [Fact]
    public async Task AutocompleteEventsAsync_ShouldReturnMappedSuggestions()
    {
        var id = Guid.NewGuid();
        var items = new List<EsAutocompleteItem>
        {
            new(id.ToString(), "Rock in Rio", "MUSIC")
        };

        _readerMock.Setup(x => x.AutocompleteAsync("roc", 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

        var engine = new ElasticsearchSearchEngine(_readerMock.Object, _options, _loggerMock.Object);

        var result = await engine.AutocompleteEventsAsync("roc", 5);

        result.Should().HaveCount(1);
        result.First().Id.Should().Be(id);
        result.First().Title.Should().Be("Rock in Rio");
        result.First().Category.Should().Be("MUSIC");
    }

    [Fact]
    public async Task GetEventByIdAsync_WhenFound_ShouldReturnMappedDomainDocument()
    {
        var id = Guid.NewGuid();
        var doc = new EsEventDocument
        {
            Id = id.ToString(),
            Title = "Lollapalooza",
            Slug = "lollapalooza",
            Category = "FESTIVAL",
            Status = "PUBLISHED",
            Venue = new EsVenue
            {
                Name = "Autodromo",
                City = "Sao Paulo",
                Location = new EsGeoLocation { Lat = -23.70, Lon = -46.69 }
            },
            Pricing = new EsPricing { MinPrice = 400, MaxPrice = 1200 }
        };

        _readerMock.Setup(x => x.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);

        var engine = new ElasticsearchSearchEngine(_readerMock.Object, _options, _loggerMock.Object);

        var result = await engine.GetEventByIdAsync(id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(id);
        result.Title.Should().Be("Lollapalooza");
        result.Category.Should().Be("FESTIVAL");
        result.MinPrice.Should().Be(400);
        result.VenueLocation.Latitude.Should().Be(-23.70);
    }

    [Fact]
    public async Task HealthCheck_WhenPingSucceeds_ShouldReturnTrue()
    {
        _readerMock.Setup(x => x.PingAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var checker = new ElasticsearchHealthChecker(_readerMock.Object, _healthLoggerMock.Object);

        var isHealthy = await checker.CheckHealthAsync();

        isHealthy.Should().BeTrue();
    }

    [Fact]
    public async Task HealthCheck_WhenPingThrows_ShouldReturnFalse()
    {
        _readerMock.Setup(x => x.PingAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("ES connection refused"));

        var checker = new ElasticsearchHealthChecker(_readerMock.Object, _healthLoggerMock.Object);

        var isHealthy = await checker.CheckHealthAsync();

        isHealthy.Should().BeFalse();
    }
}
