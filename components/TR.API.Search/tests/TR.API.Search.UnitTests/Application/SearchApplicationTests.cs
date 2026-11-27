using FluentAssertions;
using FluentValidation;
using Moq;
using TR.API.Search.Application.Features.Events.AutocompleteEvents;
using TR.API.Search.Application.Features.Events.GetEventDetails;
using TR.API.Search.Application.Features.Events.Mappings;
using TR.API.Search.Application.Features.Events.Models;
using TR.API.Search.Application.Features.Events.QueryHandlers;
using TR.API.Search.Application.Features.Events.SearchEvents;
using TR.API.Search.Application.Features.Events.Validators;
using TR.API.Search.Application.Interfaces;
using TR.API.Search.Domain.Entities;
using TR.API.Search.Domain.ValueObjects;
using Xunit;

namespace TR.API.Search.UnitTests.Application;

public class SearchApplicationTests
{
    private readonly Mock<IEventSearchEngine> _engineMock = new();

    [Fact]
    public void SearchEventsQueryValidator_WithValidQuery_ShouldBeValid()
    {
        var validator = new SearchEventsQueryValidator();
        var query = new SearchEventsQuery(
            Query: "rock",
            Category: "MUSIC",
            MinPrice: 50,
            MaxPrice: 200,
            Latitude: -23.55,
            Longitude: -46.63,
            RadiusKm: 10,
            Page: 1,
            PageSize: 20);

        var result = validator.Validate(query);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void SearchEventsQueryValidator_WhenMinPriceGreaterThanMaxPrice_ShouldHaveError()
    {
        var validator = new SearchEventsQueryValidator();
        var query = new SearchEventsQuery(MinPrice: 200, MaxPrice: 100);

        var result = validator.Validate(query);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("MinPrice cannot be greater than MaxPrice"));
    }

    [Fact]
    public void SearchEventsQueryValidator_WhenLatitudeProvidedWithoutLongitude_ShouldHaveError()
    {
        var validator = new SearchEventsQueryValidator();
        var query = new SearchEventsQuery(Latitude: -23.55, Longitude: null);

        var result = validator.Validate(query);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Both Latitude and Longitude must be provided"));
    }

    [Fact]
    public void SearchEventsQueryValidator_WhenRadiusWithoutCoordinates_ShouldHaveError()
    {
        var validator = new SearchEventsQueryValidator();
        var query = new SearchEventsQuery(RadiusKm: 15);

        var result = validator.Validate(query);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Latitude and Longitude are required when RadiusKm is specified"));
    }

    [Fact]
    public void AutocompleteEventsQueryValidator_WhenLimitOutOfRange_ShouldHaveError()
    {
        var validator = new AutocompleteEventsQueryValidator();
        var query = new AutocompleteEventsQuery("test", Limit: 100);

        var result = validator.Validate(query);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("Limit must be between 1 and 50"));
    }

    [Fact]
    public void GetEventByIdQueryValidator_WhenEmptyGuid_ShouldHaveError()
    {
        var validator = new GetEventByIdQueryValidator();
        var query = new GetEventByIdQuery(Guid.Empty);

        var result = validator.Validate(query);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("EventId cannot be empty"));
    }

    [Fact]
    public async Task SearchEventsQueryHandler_WhenValid_ShouldDelegateToEngine()
    {
        var validator = new SearchEventsQueryValidator();
        var handler = new SearchEventsQueryHandler(_engineMock.Object, validator);
        var query = new SearchEventsQuery("rock");
        var expectedResult = new EventSearchResultDto(
            Array.Empty<EventSearchItemDto>(),
            0,
            1,
            20,
            new SearchFacetsDto(Array.Empty<FacetCountDto>(), Array.Empty<FacetCountDto>()));

        _engineMock.Setup(x => x.SearchEventsAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        var result = await handler.HandleAsync(query);

        result.Should().BeSameAs(expectedResult);
        _engineMock.Verify(x => x.SearchEventsAsync(query, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SearchEventsQueryHandler_WhenInvalid_ShouldThrowValidationException()
    {
        var validator = new SearchEventsQueryValidator();
        var handler = new SearchEventsQueryHandler(_engineMock.Object, validator);
        var query = new SearchEventsQuery(Page: 0);

        var act = () => handler.HandleAsync(query);

        await act.Should().ThrowAsync<ValidationException>();
        _engineMock.Verify(x => x.SearchEventsAsync(It.IsAny<SearchEventsQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AutocompleteEventsQueryHandler_WhenPrefixEmpty_ShouldReturnEmptyWithoutCallingEngine()
    {
        var validator = new AutocompleteEventsQueryValidator();
        var handler = new AutocompleteEventsQueryHandler(_engineMock.Object, validator);
        var query = new AutocompleteEventsQuery("   ");

        var result = await handler.HandleAsync(query);

        result.Should().BeEmpty();
        _engineMock.Verify(x => x.AutocompleteEventsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AutocompleteEventsQueryHandler_WhenPrefixValid_ShouldCallEngine()
    {
        var validator = new AutocompleteEventsQueryValidator();
        var handler = new AutocompleteEventsQueryHandler(_engineMock.Object, validator);
        var query = new AutocompleteEventsQuery("roc", 5);
        var expected = new List<AutocompleteSuggestionDto>
        {
            new(Guid.NewGuid(), "Rock in Rio", "MUSIC")
        };

        _engineMock.Setup(x => x.AutocompleteEventsAsync("roc", 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await handler.HandleAsync(query);

        result.Should().BeEquivalentTo(expected);
        _engineMock.Verify(x => x.AutocompleteEventsAsync("roc", 5, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetEventByIdQueryHandler_WhenFound_ShouldMapToDto()
    {
        var validator = new GetEventByIdQueryValidator();
        var handler = new GetEventByIdQueryHandler(_engineMock.Object, validator);
        var id = Guid.NewGuid();
        var doc = new EventSearchDocument
        {
            Id = id,
            Title = "Show Especial",
            Slug = "show-especial",
            Category = "CONCERT",
            Status = "PUBLISHED",
            MinPrice = 100,
            MaxPrice = 200,
            VenueLocation = new GeoLocation(-23.55, -46.63)
        };

        _engineMock.Setup(x => x.GetEventByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(doc);

        var result = await handler.HandleAsync(new GetEventByIdQuery(id));

        result.Should().NotBeNull();
        result!.Id.Should().Be(id);
        result.Title.Should().Be("Show Especial");
        result.Category.Should().Be("CONCERT");
        result.VenueLatitude.Should().Be(-23.55);
        result.VenueLongitude.Should().Be(-46.63);
    }

    [Fact]
    public async Task GetEventByIdQueryHandler_WhenNotFound_ShouldReturnNull()
    {
        var validator = new GetEventByIdQueryValidator();
        var handler = new GetEventByIdQueryHandler(_engineMock.Object, validator);
        var id = Guid.NewGuid();

        _engineMock.Setup(x => x.GetEventByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EventSearchDocument?)null);

        var result = await handler.HandleAsync(new GetEventByIdQuery(id));

        result.Should().BeNull();
    }

    [Fact]
    public void EventSearchDtoMappingExtensions_ShouldMapFacetsCorrectly()
    {
        var facets = new SearchFacets(
            new[] { new SearchFacetEntry("MUSIC", 10), new SearchFacetEntry("THEATER", 5) },
            new[] { new SearchFacetEntry("Sao Paulo", 8) });

        var dto = facets.ToDto();

        dto.Categories.Should().HaveCount(2);
        dto.Categories.First().Key.Should().Be("MUSIC");
        dto.Categories.First().Count.Should().Be(10);
        dto.Cities.Should().HaveCount(1);
        dto.Cities.First().Key.Should().Be("Sao Paulo");
    }
}
