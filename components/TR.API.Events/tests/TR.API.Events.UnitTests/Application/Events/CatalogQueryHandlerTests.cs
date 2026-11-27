namespace TR.API.Events.UnitTests.Application.Events;

using FluentAssertions;
using Moq;
using TR.API.Events.Application.Events.GetEventById;
using TR.API.Events.Application.Events.ListEvents;
using TR.API.Events.Application.Venues.ListVenues;
using TR.API.Events.Domain.Contracts.Caching;
using TR.API.Events.Domain.Contracts.Persistence;
using TR.API.Events.Domain.Exceptions;
using Xunit;

public class CatalogQueryHandlerTests
{
    private readonly Mock<ICatalogReadRepository> _readRepositoryMock = new();
    private readonly Mock<ICacheService> _cacheServiceMock = new();

    [Fact]
    public async Task GetEventById_WhenCacheMiss_ShouldFetchFromDbAndSetCache()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var expected = new EventDetailsReadModel(
            eventId,
            Guid.NewGuid(),
            "Allianz Parque",
            "Sao Paulo",
            "Rock Show",
            "rock-show",
            "Great show",
            "CONCERT",
            "SALES_OPEN",
            now,
            now.AddDays(5),
            now.AddDays(10),
            now.AddDays(11),
            16,
            null,
            4,
            2,
            now,
            now,
            Array.Empty<EventTierReadModel>());

        var cacheKey = CacheKeys.EventById(eventId);
        _cacheServiceMock
            .Setup(c => c.GetAsync<EventDetailsReadModel>(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EventDetailsReadModel?)null);

        _readRepositoryMock
            .Setup(r => r.GetEventByIdAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var handler = new GetEventByIdQueryHandler(_readRepositoryMock.Object, _cacheServiceMock.Object);

        // Act
        var result = await handler.HandleAsync(new GetEventByIdQuery(eventId));

        // Assert
        result.Should().Be(expected);
        _cacheServiceMock.Verify(
            c => c.SetAsync(cacheKey, expected, CacheKeys.DefaultCatalogTtl, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetEventById_WhenCacheHit_ShouldReturnCachedWithoutQueryingDb()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var cachedEvent = new EventDetailsReadModel(
            eventId,
            Guid.NewGuid(),
            "Allianz Parque",
            "Sao Paulo",
            "Cached Show",
            "cached-show",
            "From Redis",
            "CONCERT",
            "SALES_OPEN",
            now,
            now.AddDays(5),
            now.AddDays(10),
            now.AddDays(11),
            16,
            null,
            4,
            2,
            now,
            now,
            Array.Empty<EventTierReadModel>());

        var cacheKey = CacheKeys.EventById(eventId);
        _cacheServiceMock
            .Setup(c => c.GetAsync<EventDetailsReadModel>(cacheKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedEvent);

        var handler = new GetEventByIdQueryHandler(_readRepositoryMock.Object, _cacheServiceMock.Object);

        // Act
        var result = await handler.HandleAsync(new GetEventByIdQuery(eventId));

        // Assert
        result.Should().Be(cachedEvent);
        _readRepositoryMock.Verify(r => r.GetEventByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetEventById_WhenEventDoesNotExist_ShouldThrowNotFoundException()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        _readRepositoryMock
            .Setup(r => r.GetEventByIdAsync(eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EventDetailsReadModel?)null);

        var handler = new GetEventByIdQueryHandler(_readRepositoryMock.Object, _cacheServiceMock.Object);

        // Act
        var act = async () => await handler.HandleAsync(new GetEventByIdQuery(eventId));

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ListEvents_ShouldCalculatePaginationMetadataCorrectly()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var items = new List<EventSummaryReadModel>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), "Allianz Parque", "Sao Paulo", "Show 1", "show-1", "CONCERT", "SALES_OPEN", now, now.AddDays(1), now.AddDays(2), now.AddDays(3), 0, null)
        }.AsReadOnly();

        _readRepositoryMock
            .Setup(r => r.ListEventsAsync(1, 10, "CONCERT", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((items, 25));

        var handler = new ListEventsQueryHandler(_readRepositoryMock.Object);

        // Act
        var result = await handler.HandleAsync(new ListEventsQuery(1, 10, "CONCERT", null, null));

        // Assert
        result.Items.Should().HaveCount(1);
        result.TotalCount.Should().Be(25);
        result.TotalPages.Should().Be(3);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task ListVenues_WhenCacheMiss_ShouldReturnVenuesAndPopulateCache()
    {
        // Arrange
        IReadOnlyCollection<VenueReadModel> venues = new List<VenueReadModel>
        {
            new(Guid.NewGuid(), "Allianz Parque", "allianz-parque", "Arena", "Av. Francisco Matarazzo", "Sao Paulo", "SP", "BR", 45000, Array.Empty<SeatSectionReadModel>())
        }.AsReadOnly();

        _readRepositoryMock
            .Setup(r => r.ListVenuesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(venues);

        var handler = new ListVenuesQueryHandler(_readRepositoryMock.Object, _cacheServiceMock.Object);

        // Act
        var result = await handler.HandleAsync(new ListVenuesQuery());

        // Assert
        result.Should().HaveCount(1);
        result.First().Name.Should().Be("Allianz Parque");
        _cacheServiceMock.Verify(
            c => c.SetAsync(CacheKeys.VenuesList(), venues, CacheKeys.DefaultCatalogTtl, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
