namespace TR.API.Events.Domain.Entities;

using TR.API.Events.Domain.Exceptions;

public class Venue
{
    private readonly List<SeatSection> _sections = new();

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string Address { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;
    public string State { get; private set; } = string.Empty;
    public string Country { get; private set; } = "BR";
    public string PostalCode { get; private set; } = string.Empty;
    public string Timezone { get; private set; } = "America/Sao_Paulo";
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }
    public int TotalCapacity { get; private set; }
    public string Metadata { get; private set; } = "{}";
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<SeatSection> Sections => _sections.AsReadOnly();

    private Venue() { }

    public static Venue Create(
        string name,
        string slug,
        string address,
        string city,
        string state,
        string postalCode,
        int totalCapacity,
        string? description = null,
        string country = "BR",
        string timezone = "America/Sao_Paulo",
        decimal? latitude = null,
        decimal? longitude = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Venue name cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new DomainException("Venue slug cannot be empty.");
        }

        if (totalCapacity <= 0)
        {
            throw new DomainException("Venue total capacity must be greater than zero.");
        }

        var now = DateTimeOffset.UtcNow;

        return new Venue
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Slug = slug.Trim().ToLowerInvariant(),
            Description = description?.Trim(),
            Address = address.Trim(),
            City = city.Trim(),
            State = state.Trim(),
            Country = country.Trim(),
            PostalCode = postalCode.Trim(),
            Timezone = timezone.Trim(),
            Latitude = latitude,
            Longitude = longitude,
            TotalCapacity = totalCapacity,
            Metadata = "{}",
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}
