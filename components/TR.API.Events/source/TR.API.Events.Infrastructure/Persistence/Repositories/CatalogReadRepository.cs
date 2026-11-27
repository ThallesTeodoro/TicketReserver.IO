namespace TR.API.Events.Infrastructure.Persistence.Repositories;

using Dapper;
using TR.API.Events.Domain.Contracts.Persistence;

public sealed class CatalogReadRepository(IDbConnectionFactory connectionFactory) : ICatalogReadRepository
{
    public async Task<EventDetailsReadModel?> GetEventByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string eventSql = """
            SELECT
                e.id AS Id,
                e.venue_id AS VenueId,
                v.name AS VenueName,
                v.city AS VenueCity,
                e.title AS Title,
                e.slug AS Slug,
                e.description AS Description,
                e.category AS Category,
                e.status::text AS Status,
                e.sales_start_at AS SalesStartAt,
                e.sales_end_at AS SalesEndAt,
                e.event_start_at AS EventStartAt,
                e.event_end_at AS EventEndAt,
                e.age_restriction AS AgeRestriction,
                e.banner_url AS BannerUrl,
                e.max_tickets_per_user AS MaxTicketsPerUser,
                e.version AS Version,
                e.created_at AS CreatedAt,
                e.updated_at AS UpdatedAt
            FROM events e
            INNER JOIN venues v ON v.id = e.venue_id
            WHERE e.id = @Id;
            """;

        const string tiersSql = """
            SELECT
                t.id AS Id,
                t.section_id AS SectionId,
                s.name AS SectionName,
                t.name AS Name,
                t.tier_type::text AS TierType,
                t.price AS Price,
                t.currency AS Currency,
                t.total_quantity AS TotalQuantity,
                t.sales_start_at AS SalesStartAt,
                t.sales_end_at AS SalesEndAt,
                t.status::text AS Status
            FROM ticket_tiers t
            INNER JOIN seat_sections s ON s.id = t.section_id
            WHERE t.event_id = @EventId
            ORDER BY t.price ASC;
            """;

        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<EventDetailsRow>(
            new CommandDefinition(eventSql, new { Id = id }, cancellationToken: cancellationToken));

        if (row is null)
        {
            return null;
        }

        var tierRows = await connection.QueryAsync<EventTierRow>(
            new CommandDefinition(tiersSql, new { EventId = id }, cancellationToken: cancellationToken));

        var tiers = tierRows.Select(t => new EventTierReadModel(
            t.Id,
            t.SectionId,
            t.SectionName,
            t.Name,
            t.TierType,
            t.Price,
            t.Currency,
            t.TotalQuantity,
            ToUtcOffset(t.SalesStartAt),
            ToUtcOffset(t.SalesEndAt),
            t.Status)).ToList().AsReadOnly();

        return new EventDetailsReadModel(
            row.Id,
            row.VenueId,
            row.VenueName,
            row.VenueCity,
            row.Title,
            row.Slug,
            row.Description,
            row.Category,
            row.Status,
            ToUtcOffset(row.SalesStartAt),
            ToUtcOffset(row.SalesEndAt),
            ToUtcOffset(row.EventStartAt),
            ToUtcOffset(row.EventEndAt),
            row.AgeRestriction,
            row.BannerUrl,
            row.MaxTicketsPerUser,
            row.Version,
            ToUtcOffset(row.CreatedAt),
            ToUtcOffset(row.UpdatedAt),
            tiers);
    }

    public async Task<(IReadOnlyCollection<EventSummaryReadModel> Items, int TotalCount)> ListEventsAsync(
        int page,
        int pageSize,
        string? category,
        string? status,
        string? city,
        CancellationToken cancellationToken = default)
    {
        const string countSql = """
            SELECT COUNT(1)
            FROM events e
            INNER JOIN venues v ON v.id = e.venue_id
            WHERE (@Category IS NULL OR e.category = @Category)
              AND (@Status IS NULL OR e.status::text = @Status)
              AND (@City IS NULL OR v.city ILIKE @City);
            """;

        const string itemsSql = """
            SELECT
                e.id AS Id,
                e.venue_id AS VenueId,
                v.name AS VenueName,
                v.city AS VenueCity,
                e.title AS Title,
                e.slug AS Slug,
                e.category AS Category,
                e.status::text AS Status,
                e.sales_start_at AS SalesStartAt,
                e.sales_end_at AS SalesEndAt,
                e.event_start_at AS EventStartAt,
                e.event_end_at AS EventEndAt,
                e.age_restriction AS AgeRestriction,
                e.banner_url AS BannerUrl
            FROM events e
            INNER JOIN venues v ON v.id = e.venue_id
            WHERE (@Category IS NULL OR e.category = @Category)
              AND (@Status IS NULL OR e.status::text = @Status)
              AND (@City IS NULL OR v.city ILIKE @City)
            ORDER BY e.event_start_at ASC
            OFFSET @Offset LIMIT @Limit;
            """;

        var parameters = new
        {
            Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim().ToUpperInvariant(),
            Status = string.IsNullOrWhiteSpace(status) ? null : status.Trim().ToUpperInvariant(),
            City = string.IsNullOrWhiteSpace(city) ? null : city.Trim(),
            Offset = (page - 1) * pageSize,
            Limit = pageSize
        };

        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);

        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(countSql, parameters, cancellationToken: cancellationToken));

        var rows = await connection.QueryAsync<EventSummaryRow>(
            new CommandDefinition(itemsSql, parameters, cancellationToken: cancellationToken));

        var items = rows.Select(r => new EventSummaryReadModel(
            r.Id,
            r.VenueId,
            r.VenueName,
            r.VenueCity,
            r.Title,
            r.Slug,
            r.Category,
            r.Status,
            ToUtcOffset(r.SalesStartAt),
            ToUtcOffset(r.SalesEndAt),
            ToUtcOffset(r.EventStartAt),
            ToUtcOffset(r.EventEndAt),
            r.AgeRestriction,
            r.BannerUrl)).ToList().AsReadOnly();

        return (items, totalCount);
    }

    public async Task<IReadOnlyCollection<VenueReadModel>> ListVenuesAsync(CancellationToken cancellationToken = default)
    {
        const string venuesSql = """
            SELECT
                v.id AS Id,
                v.name AS Name,
                v.slug AS Slug,
                v.description AS Description,
                v.address AS Address,
                v.city AS City,
                v.state AS State,
                v.country AS Country,
                v.total_capacity AS TotalCapacity
            FROM venues v
            ORDER BY v.name ASC;
            """;

        const string sectionsSql = """
            SELECT
                s.id AS Id,
                s.venue_id AS VenueId,
                s.name AS Name,
                s.code AS Code,
                s.is_numbered AS IsNumbered,
                s.capacity AS Capacity,
                s.display_order AS DisplayOrder
            FROM seat_sections s
            ORDER BY s.display_order ASC, s.name ASC;
            """;

        using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);

        var venueRows = await connection.QueryAsync<VenueRow>(
            new CommandDefinition(venuesSql, cancellationToken: cancellationToken));

        var sectionRows = await connection.QueryAsync<SeatSectionReadModel>(
            new CommandDefinition(sectionsSql, cancellationToken: cancellationToken));

        var sectionsByVenue = sectionRows
            .GroupBy(s => s.VenueId)
            .ToDictionary(g => g.Key, g => (IReadOnlyCollection<SeatSectionReadModel>)g.ToList().AsReadOnly());

        return venueRows.Select(v => new VenueReadModel(
            v.Id,
            v.Name,
            v.Slug,
            v.Description,
            v.Address,
            v.City,
            v.State,
            v.Country,
            v.TotalCapacity,
            sectionsByVenue.TryGetValue(v.Id, out var secs) ? secs : Array.Empty<SeatSectionReadModel>()))
            .ToList()
            .AsReadOnly();
    }

    private static DateTimeOffset ToUtcOffset(DateTime dt)
    {
        return new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc));
    }

    private sealed class EventDetailsRow
    {
        public Guid Id { get; init; }
        public Guid VenueId { get; init; }
        public string VenueName { get; init; } = string.Empty;
        public string VenueCity { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Slug { get; init; } = string.Empty;
        public string? Description { get; init; }
        public string Category { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public DateTime SalesStartAt { get; init; }
        public DateTime SalesEndAt { get; init; }
        public DateTime EventStartAt { get; init; }
        public DateTime EventEndAt { get; init; }
        public int AgeRestriction { get; init; }
        public string? BannerUrl { get; init; }
        public int MaxTicketsPerUser { get; init; }
        public int Version { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime UpdatedAt { get; init; }
    }

    private sealed class EventTierRow
    {
        public Guid Id { get; init; }
        public Guid SectionId { get; init; }
        public string SectionName { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string TierType { get; init; } = string.Empty;
        public decimal Price { get; init; }
        public string Currency { get; init; } = string.Empty;
        public int TotalQuantity { get; init; }
        public DateTime SalesStartAt { get; init; }
        public DateTime SalesEndAt { get; init; }
        public string Status { get; init; } = string.Empty;
    }

    private sealed class EventSummaryRow
    {
        public Guid Id { get; init; }
        public Guid VenueId { get; init; }
        public string VenueName { get; init; } = string.Empty;
        public string VenueCity { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Slug { get; init; } = string.Empty;
        public string Category { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public DateTime SalesStartAt { get; init; }
        public DateTime SalesEndAt { get; init; }
        public DateTime EventStartAt { get; init; }
        public DateTime EventEndAt { get; init; }
        public int AgeRestriction { get; init; }
        public string? BannerUrl { get; init; }
    }

    private sealed class VenueRow
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Slug { get; init; } = string.Empty;
        public string? Description { get; init; }
        public string Address { get; init; } = string.Empty;
        public string City { get; init; } = string.Empty;
        public string State { get; init; } = string.Empty;
        public string Country { get; init; } = string.Empty;
        public int TotalCapacity { get; init; }
    }
}
