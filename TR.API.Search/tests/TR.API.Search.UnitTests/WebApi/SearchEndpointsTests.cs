using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using TR.API.Search.Application.Common.Messaging;
using TR.API.Search.Application.Features.Events.AutocompleteEvents;
using TR.API.Search.Application.Features.Events.GetEventDetails;
using TR.API.Search.Application.Features.Events.Models;
using TR.API.Search.Application.Features.Events.SearchEvents;
using TR.API.Search.Infrastructure.Health;
using TR.API.Search.WebApi.Contracts;
using Xunit;

namespace TR.API.Search.UnitTests.WebApi;

public class SearchEndpointsTests
{
    private readonly Mock<IQueryHandler<SearchEventsQuery, EventSearchResultDto>> _searchHandlerMock = new();
    private readonly Mock<IQueryHandler<AutocompleteEventsQuery, IReadOnlyCollection<AutocompleteSuggestionDto>>> _autocompleteHandlerMock = new();
    private readonly Mock<IQueryHandler<GetEventByIdQuery, EventSearchItemDto?>> _getByIdHandlerMock = new();
    private readonly Mock<IElasticsearchHealthChecker> _healthCheckerMock = new();

    [Fact]
    public async Task SearchEvents_Endpoint_ShouldReturnOkWithEnvelope()
    {
        var expectedResult = new EventSearchResultDto(
            Array.Empty<EventSearchItemDto>(),
            0,
            1,
            20,
            new SearchFacetsDto(Array.Empty<FacetCountDto>(), Array.Empty<FacetCountDto>()));

        _searchHandlerMock.Setup(x => x.HandleAsync(It.IsAny<SearchEventsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var query = new SearchEventsQuery("rock");
        var result = await _searchHandlerMock.Object.HandleAsync(query);

        result.Should().BeSameAs(expectedResult);
    }

    [Fact]
    public async Task AutocompleteEvents_Endpoint_ShouldReturnOkWithSuggestions()
    {
        var suggestions = new List<AutocompleteSuggestionDto>
        {
            new(Guid.NewGuid(), "Rock Fest", "MUSIC")
        };

        _autocompleteHandlerMock.Setup(x => x.HandleAsync(It.IsAny<AutocompleteEventsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(suggestions);

        var result = await _autocompleteHandlerMock.Object.HandleAsync(new AutocompleteEventsQuery("roc"));

        result.Should().BeEquivalentTo(suggestions);
    }

    [Fact]
    public async Task GetEventById_Endpoint_WhenFound_ShouldReturnDto()
    {
        var id = Guid.NewGuid();
        var item = new EventSearchItemDto(
            id, "Show", "show", "desc", "MUSIC", "PUBLISHED", 18, null,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            Guid.NewGuid(), "Venue", "City", "ST", "BR", 0, 0, 100, 200,
            Array.Empty<string>(), Array.Empty<TicketTierSummaryDto>());

        _getByIdHandlerMock.Setup(x => x.HandleAsync(It.Is<GetEventByIdQuery>(q => q.EventId == id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(item);

        var result = await _getByIdHandlerMock.Object.HandleAsync(new GetEventByIdQuery(id));

        result.Should().NotBeNull();
        result!.Id.Should().Be(id);
    }

    [Fact]
    public async Task GetEventById_Endpoint_WhenNotFound_ShouldReturnNull()
    {
        var id = Guid.NewGuid();
        _getByIdHandlerMock.Setup(x => x.HandleAsync(It.IsAny<GetEventByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EventSearchItemDto?)null);

        var result = await _getByIdHandlerMock.Object.HandleAsync(new GetEventByIdQuery(id));

        result.Should().BeNull();
    }

    [Fact]
    public async Task HealthCheck_WhenHealthy_ShouldReturnTrue()
    {
        _healthCheckerMock.Setup(x => x.CheckHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var isHealthy = await _healthCheckerMock.Object.CheckHealthAsync();

        isHealthy.Should().BeTrue();
    }

    [Fact]
    public async Task HealthCheck_WhenUnhealthy_ShouldReturnFalse()
    {
        _healthCheckerMock.Setup(x => x.CheckHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var isHealthy = await _healthCheckerMock.Object.CheckHealthAsync();

        isHealthy.Should().BeFalse();
    }
}
