namespace TR.API.Events.Domain.Entities;

using TR.API.Events.Domain.Exceptions;

public class SeatSection
{
    public Guid Id { get; private set; }
    public Guid VenueId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public bool IsNumbered { get; private set; }
    public int Capacity { get; private set; }
    public int DisplayOrder { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private SeatSection() { }

    public static SeatSection Create(
        Guid venueId,
        string name,
        string code,
        int capacity,
        bool isNumbered = false,
        int displayOrder = 0)
    {
        if (venueId == Guid.Empty)
        {
            throw new DomainException("Venue ID cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Seat section name cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainException("Seat section code cannot be empty.");
        }

        if (capacity <= 0)
        {
            throw new DomainException("Seat section capacity must be greater than zero.");
        }

        var now = DateTimeOffset.UtcNow;

        return new SeatSection
        {
            Id = Guid.NewGuid(),
            VenueId = venueId,
            Name = name.Trim(),
            Code = code.Trim().ToUpperInvariant(),
            IsNumbered = isNumbered,
            Capacity = capacity,
            DisplayOrder = displayOrder,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}
