namespace TR.HW.Repl.Domain.ValueObjects;

public sealed record GeoLocation(double Lat, double Lon);

public sealed record EventScheduleInfo(
    DateTimeOffset SalesStartAt,
    DateTimeOffset SalesEndAt,
    DateTimeOffset EventStartAt,
    DateTimeOffset EventEndAt);

public sealed record VenueSearchInfo(
    Guid Id,
    string Name,
    string City,
    string State,
    string Country,
    GeoLocation Location);

public sealed record PricingSummary(
    decimal MinPrice,
    decimal MaxPrice,
    string Currency = "BRL");
