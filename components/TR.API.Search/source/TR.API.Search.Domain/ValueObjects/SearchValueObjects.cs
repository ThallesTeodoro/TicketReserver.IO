using TR.API.Search.Domain.Exceptions;

namespace TR.API.Search.Domain.ValueObjects;

public enum SearchSortOption
{
    Relevance,
    DateAsc,
    DateDesc,
    PriceAsc,
    PriceDesc,
    Distance
}

public sealed record GeoLocation
{
    public double Latitude { get; }
    public double Longitude { get; }

    public GeoLocation(double latitude, double longitude)
    {
        if (latitude is < -90 or > 90)
            throw new DomainException($"Latitude must be between -90 and 90. Received: {latitude}");

        if (longitude is < -180 or > 180)
            throw new DomainException($"Longitude must be between -180 and 180. Received: {longitude}");

        Latitude = latitude;
        Longitude = longitude;
    }

    public double DistanceToKm(GeoLocation other)
    {
        ArgumentNullException.ThrowIfNull(other);

        const double earthRadiusKm = 6371.0;

        var dLat = DegreesToRadians(other.Latitude - Latitude);
        var dLon = DegreesToRadians(other.Longitude - Longitude);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(DegreesToRadians(Latitude)) * Math.Cos(DegreesToRadians(other.Latitude)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusKm * c;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;
}

public sealed record PriceRangeFilter
{
    public decimal? MinPrice { get; }
    public decimal? MaxPrice { get; }

    public PriceRangeFilter(decimal? minPrice, decimal? maxPrice)
    {
        if (minPrice < 0)
            throw new DomainException("Minimum price cannot be negative.");

        if (maxPrice < 0)
            throw new DomainException("Maximum price cannot be negative.");

        if (minPrice.HasValue && maxPrice.HasValue && minPrice.Value > maxPrice.Value)
            throw new DomainException($"Minimum price ({minPrice}) cannot be greater than maximum price ({maxPrice}).");

        MinPrice = minPrice;
        MaxPrice = maxPrice;
    }

    public bool HasFilter => MinPrice.HasValue || MaxPrice.HasValue;
}

public sealed record DateRangeFilter
{
    public DateTimeOffset? FromDate { get; }
    public DateTimeOffset? ToDate { get; }

    public DateRangeFilter(DateTimeOffset? fromDate, DateTimeOffset? toDate)
    {
        if (fromDate.HasValue && toDate.HasValue && fromDate.Value > toDate.Value)
            throw new DomainException($"From date ({fromDate}) cannot be greater than to date ({toDate}).");

        FromDate = fromDate;
        ToDate = toDate;
    }

    public bool HasFilter => FromDate.HasValue || ToDate.HasValue;
}

public sealed record Pagination
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public int Page { get; }
    public int PageSize { get; }
    public int Skip => (Page - 1) * PageSize;

    public Pagination(int page = DefaultPage, int pageSize = DefaultPageSize)
    {
        if (page < 1)
            throw new DomainException("Page number must be greater than or equal to 1.");

        if (pageSize < 1)
            throw new DomainException("Page size must be greater than or equal to 1.");

        if (pageSize > MaxPageSize)
            throw new DomainException($"Page size cannot exceed {MaxPageSize}. Received: {pageSize}");

        Page = page;
        PageSize = pageSize;
    }
}
