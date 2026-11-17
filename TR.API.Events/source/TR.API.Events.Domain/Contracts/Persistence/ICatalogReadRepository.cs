namespace TR.API.Events.Domain.Contracts.Persistence;

public sealed record EventTierReadModel(
    Guid Id,
    Guid SectionId,
    string SectionName,
    string Name,
    string TierType,
    decimal Price,
    string Currency,
    int TotalQuantity,
    DateTimeOffset SalesStartAt,
    DateTimeOffset SalesEndAt,
    string Status);

public sealed record EventDetailsReadModel(
    Guid Id,
    Guid VenueId,
    string VenueName,
    string VenueCity,
    string Title,
    string Slug,
    string? Description,
    string Category,
    string Status,
    DateTimeOffset SalesStartAt,
    DateTimeOffset SalesEndAt,
    DateTimeOffset EventStartAt,
    DateTimeOffset EventEndAt,
    int AgeRestriction,
    string? BannerUrl,
    int MaxTicketsPerUser,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyCollection<EventTierReadModel> TicketTiers);

public sealed record EventSummaryReadModel(
    Guid Id,
    Guid VenueId,
    string VenueName,
    string VenueCity,
    string Title,
    string Slug,
    string Category,
    string Status,
    DateTimeOffset SalesStartAt,
    DateTimeOffset SalesEndAt,
    DateTimeOffset EventStartAt,
    DateTimeOffset EventEndAt,
    int AgeRestriction,
    string? BannerUrl);

public sealed record SeatSectionReadModel(
    Guid Id,
    Guid VenueId,
    string Name,
    string Code,
    bool IsNumbered,
    int Capacity,
    int DisplayOrder);

public sealed record VenueReadModel(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    string Address,
    string City,
    string State,
    string Country,
    int TotalCapacity,
    IReadOnlyCollection<SeatSectionReadModel> Sections);

public interface ICatalogReadRepository
{
    Task<EventDetailsReadModel?> GetEventByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(IReadOnlyCollection<EventSummaryReadModel> Items, int TotalCount)> ListEventsAsync(
        int page,
        int pageSize,
        string? category,
        string? status,
        string? city,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<VenueReadModel>> ListVenuesAsync(CancellationToken cancellationToken = default);
}
