using FluentAssertions;
using TR.API.Search.Domain.Exceptions;
using TR.API.Search.Domain.ValueObjects;
using Xunit;

namespace TR.API.Search.UnitTests.Domain;

public class SearchDomainTests
{
    [Theory]
    [InlineData(-23.5505, -46.6333)]
    [InlineData(0, 0)]
    [InlineData(90, 180)]
    [InlineData(-90, -180)]
    public void GeoLocation_WithValidCoordinates_ShouldInstantiate(double lat, double lon)
    {
        var geo = new GeoLocation(lat, lon);
        geo.Latitude.Should().Be(lat);
        geo.Longitude.Should().Be(lon);
    }

    [Theory]
    [InlineData(-91, 0)]
    [InlineData(91, 0)]
    [InlineData(0, -181)]
    [InlineData(0, 181)]
    public void GeoLocation_WithInvalidCoordinates_ShouldThrowDomainException(double lat, double lon)
    {
        var act = () => new GeoLocation(lat, lon);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void GeoLocation_DistanceToKm_BetweenSaoPauloAndRio_ShouldBeApproximately357Km()
    {
        // São Paulo
        var sp = new GeoLocation(-23.5505, -46.6333);
        // Rio de Janeiro
        var rj = new GeoLocation(-22.9068, -43.1729);

        var distance = sp.DistanceToKm(rj);

        distance.Should().BeInRange(350, 365);
    }

    [Fact]
    public void PriceRangeFilter_WithValidPrices_ShouldSetProperties()
    {
        var filter = new PriceRangeFilter(50m, 150m);
        filter.MinPrice.Should().Be(50m);
        filter.MaxPrice.Should().Be(150m);
        filter.HasFilter.Should().BeTrue();
    }

    [Fact]
    public void PriceRangeFilter_WhenMinGreaterThanMax_ShouldThrowDomainException()
    {
        var act = () => new PriceRangeFilter(200m, 100m);
        act.Should().Throw<DomainException>()
            .WithMessage("*cannot be greater than maximum price*");
    }

    [Fact]
    public void PriceRangeFilter_WhenNegative_ShouldThrowDomainException()
    {
        var act = () => new PriceRangeFilter(-10m, 100m);
        act.Should().Throw<DomainException>()
            .WithMessage("*cannot be negative*");
    }

    [Fact]
    public void DateRangeFilter_WithValidDates_ShouldSetProperties()
    {
        var from = DateTimeOffset.UtcNow;
        var to = from.AddDays(7);
        var filter = new DateRangeFilter(from, to);

        filter.FromDate.Should().Be(from);
        filter.ToDate.Should().Be(to);
        filter.HasFilter.Should().BeTrue();
    }

    [Fact]
    public void DateRangeFilter_WhenFromGreaterThanTo_ShouldThrowDomainException()
    {
        var from = DateTimeOffset.UtcNow.AddDays(10);
        var to = DateTimeOffset.UtcNow;

        var act = () => new DateRangeFilter(from, to);
        act.Should().Throw<DomainException>()
            .WithMessage("*cannot be greater than to date*");
    }

    [Theory]
    [InlineData(1, 20, 0)]
    [InlineData(2, 20, 20)]
    [InlineData(3, 10, 20)]
    public void Pagination_WithValidValues_ShouldCalculateSkipCorrectly(int page, int pageSize, int expectedSkip)
    {
        var pagination = new Pagination(page, pageSize);
        pagination.Page.Should().Be(page);
        pagination.PageSize.Should().Be(pageSize);
        pagination.Skip.Should().Be(expectedSkip);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void Pagination_WithInvalidValues_ShouldThrowDomainException(int page, int pageSize)
    {
        var act = () => new Pagination(page, pageSize);
        act.Should().Throw<DomainException>();
    }
}
