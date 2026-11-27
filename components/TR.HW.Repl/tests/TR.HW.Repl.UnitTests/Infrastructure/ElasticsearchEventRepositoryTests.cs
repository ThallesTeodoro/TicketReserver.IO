using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TR.HW.Repl.Domain.Entities;
using TR.HW.Repl.Domain.ValueObjects;
using TR.HW.Repl.Infrastructure.Configuration;
using TR.HW.Repl.Infrastructure.Persistence;

namespace TR.HW.Repl.UnitTests.Infrastructure;

public class ElasticsearchEventRepositoryTests
{
    private readonly Mock<IElasticsearchEngine> _engineMock;
    private readonly Mock<ILogger<ElasticsearchEventRepository>> _loggerMock;
    private readonly IOptions<ElasticsearchOptions> _options;
    private readonly ElasticsearchEventRepository _repository;

    public ElasticsearchEventRepositoryTests()
    {
        _engineMock = new Mock<IElasticsearchEngine>();
        _loggerMock = new Mock<ILogger<ElasticsearchEventRepository>>();
        _options = Microsoft.Extensions.Options.Options.Create(new ElasticsearchOptions
        {
            IndexName = "events_search",
            RetryCount = 2
        });

        _repository = new ElasticsearchEventRepository(_engineMock.Object, _options, _loggerMock.Object);
    }

    private static EventSearchDocument CreateSampleDocument(Guid id)
    {
        return EventSearchDocument.Create(
            id: id,
            title: "Test Event",
            slug: "test-event",
            description: "Description",
            category: "CONCERT",
            status: "SALES_OPEN",
            ageRestriction: 18,
            bannerUrl: null,
            schedule: new EventScheduleInfo(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(2), DateTimeOffset.UtcNow.AddDays(2).AddHours(2)),
            venue: new VenueSearchInfo(Guid.NewGuid(), "Venue", "City", "ST", "BR", new GeoLocation(0, 0)));
    }

    [Fact]
    public async Task UpsertEventAsync_ShouldInvokeEngineWithConfiguredIndex()
    {
        // Arrange
        var doc = CreateSampleDocument(Guid.NewGuid());

        // Act
        await _repository.UpsertEventAsync(doc);

        // Assert
        _engineMock.Verify(e => e.IndexDocumentAsync("events_search", doc, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateEventStatusAsync_ShouldInvokeUpdateDocumentPartialAsync()
    {
        // Arrange
        var eventId = Guid.NewGuid();

        // Act
        await _repository.UpdateEventStatusAsync(eventId, "CANCELLED", DateTimeOffset.UtcNow);

        // Assert
        _engineMock.Verify(e => e.UpdateDocumentPartialAsync("events_search", eventId, It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteEventAsync_ShouldInvokeDeleteDocumentAsync()
    {
        // Arrange
        var eventId = Guid.NewGuid();

        // Act
        await _repository.DeleteEventAsync(eventId);

        // Assert
        _engineMock.Verify(e => e.DeleteDocumentAsync("events_search", eventId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateTierPricingAsync_WhenEventExists_ShouldUpsertTierAndReindex()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var doc = CreateSampleDocument(eventId);
        var tier = TicketTierSummary.Create(Guid.NewGuid(), "Pista", 150m);

        _engineMock
            .Setup(e => e.GetDocumentAsync("events_search", eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);

        // Act
        await _repository.UpdateTierPricingAsync(eventId, tier);

        // Assert
        _engineMock.Verify(e => e.IndexDocumentAsync(
            "events_search",
            It.Is<EventSearchDocument>(d => d.Id == eventId && d.Pricing.MinPrice == 150m),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpsertEventAsync_WhenTransientFailureOccurs_ShouldRetryAndSucceed()
    {
        // Arrange
        var doc = CreateSampleDocument(Guid.NewGuid());
        var attempt = 0;

        _engineMock
            .Setup(e => e.IndexDocumentAsync("events_search", doc, It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                attempt++;
                if (attempt == 1)
                    throw new InvalidOperationException("Connection timeout to Elastic cluster");
                return Task.CompletedTask;
            });

        // Act
        await _repository.UpsertEventAsync(doc);

        // Assert
        attempt.Should().Be(2);
        _engineMock.Verify(e => e.IndexDocumentAsync("events_search", doc, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }
}
