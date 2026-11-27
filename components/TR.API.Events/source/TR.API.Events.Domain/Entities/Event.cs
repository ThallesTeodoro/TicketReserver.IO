namespace TR.API.Events.Domain.Entities;

using TR.API.Events.Domain.Enums;
using TR.API.Events.Domain.Exceptions;
using TR.API.Events.Domain.ValueObjects;

public class Event
{
    private readonly List<TicketTier> _ticketTiers = new();

    public Guid Id { get; private set; }
    public Guid VenueId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string Category { get; private set; } = string.Empty;
    public EventStatus Status { get; private set; } = EventStatus.DRAFT;
    public DateTimeOffset SalesStartAt { get; private set; }
    public DateTimeOffset SalesEndAt { get; private set; }
    public DateTimeOffset EventStartAt { get; private set; }
    public DateTimeOffset EventEndAt { get; private set; }
    public int AgeRestriction { get; private set; }
    public string? BannerUrl { get; private set; }
    public int MaxTicketsPerUser { get; private set; } = 4;
    public int Version { get; private set; } = 1;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<TicketTier> TicketTiers => _ticketTiers.AsReadOnly();

    private Event() { }

    public static Event Create(
        Guid venueId,
        string title,
        string slug,
        string? description,
        string category,
        DateTimeRange salesWindow,
        DateTimeRange schedule,
        int ageRestriction = 0,
        string? bannerUrl = null,
        int maxTicketsPerUser = 4)
    {
        ValidateInvariants(venueId, title, slug, category, salesWindow, schedule, ageRestriction, maxTicketsPerUser);

        var now = DateTimeOffset.UtcNow;

        return new Event
        {
            Id = Guid.NewGuid(),
            VenueId = venueId,
            Title = title.Trim(),
            Slug = slug.Trim().ToLowerInvariant(),
            Description = description?.Trim(),
            Category = category.Trim().ToUpperInvariant(),
            Status = EventStatus.DRAFT,
            SalesStartAt = salesWindow.Start,
            SalesEndAt = salesWindow.End,
            EventStartAt = schedule.Start,
            EventEndAt = schedule.End,
            AgeRestriction = ageRestriction,
            BannerUrl = bannerUrl?.Trim(),
            MaxTicketsPerUser = maxTicketsPerUser,
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void UpdateDetails(
        Guid venueId,
        string title,
        string slug,
        string? description,
        string category,
        DateTimeRange salesWindow,
        DateTimeRange schedule,
        int ageRestriction,
        string? bannerUrl,
        int maxTicketsPerUser)
    {
        if (Status is EventStatus.CANCELLED or EventStatus.FINISHED)
        {
            throw new DomainException($"Cannot update an event in status '{Status}'.");
        }

        ValidateInvariants(venueId, title, slug, category, salesWindow, schedule, ageRestriction, maxTicketsPerUser);

        VenueId = venueId;
        Title = title.Trim();
        Slug = slug.Trim().ToLowerInvariant();
        Description = description?.Trim();
        Category = category.Trim().ToUpperInvariant();
        SalesStartAt = salesWindow.Start;
        SalesEndAt = salesWindow.End;
        EventStartAt = schedule.Start;
        EventEndAt = schedule.End;
        AgeRestriction = ageRestriction;
        BannerUrl = bannerUrl?.Trim();
        MaxTicketsPerUser = maxTicketsPerUser;

        IncrementVersion();
    }

    public TicketTier AddTicketTier(
        Guid sectionId,
        string name,
        TicketTierType tierType,
        Money price,
        int totalQuantity,
        DateTimeRange salesWindow)
    {
        if (Status is EventStatus.CANCELLED or EventStatus.FINISHED)
        {
            throw new DomainException($"Cannot add ticket tiers to an event in status '{Status}'.");
        }

        var tier = TicketTier.Create(Id, sectionId, name, tierType, price, totalQuantity, salesWindow);
        _ticketTiers.Add(tier);
        IncrementVersion();

        return tier;
    }

    public void Publish()
    {
        if (Status != EventStatus.DRAFT)
        {
            throw new DomainException($"Cannot publish event from status '{Status}'. Only DRAFT events can be published.");
        }

        Status = EventStatus.PUBLISHED;
        IncrementVersion();
    }

    public void OpenSales()
    {
        if (Status is not (EventStatus.PUBLISHED or EventStatus.SALES_PAUSED))
        {
            throw new DomainException($"Cannot open sales from status '{Status}'. Event must be PUBLISHED or SALES_PAUSED.");
        }

        Status = EventStatus.SALES_OPEN;
        IncrementVersion();
    }

    public void PauseSales()
    {
        if (Status != EventStatus.SALES_OPEN)
        {
            throw new DomainException($"Cannot pause sales from status '{Status}'. Event must be in SALES_OPEN status.");
        }

        Status = EventStatus.SALES_PAUSED;
        IncrementVersion();
    }

    public void MarkAsSoldOut()
    {
        if (Status != EventStatus.SALES_OPEN)
        {
            throw new DomainException($"Cannot mark as sold out from status '{Status}'. Event must be in SALES_OPEN status.");
        }

        Status = EventStatus.SOLD_OUT;
        IncrementVersion();
    }

    public void Cancel()
    {
        if (Status is EventStatus.CANCELLED or EventStatus.FINISHED)
        {
            throw new DomainException($"Cannot cancel event from status '{Status}'.");
        }

        Status = EventStatus.CANCELLED;
        IncrementVersion();
    }

    public void TransitionTo(EventStatus targetStatus)
    {
        switch (targetStatus)
        {
            case EventStatus.PUBLISHED:
                Publish();
                break;
            case EventStatus.SALES_OPEN:
                OpenSales();
                break;
            case EventStatus.SALES_PAUSED:
                PauseSales();
                break;
            case EventStatus.SOLD_OUT:
                MarkAsSoldOut();
                break;
            case EventStatus.CANCELLED:
                Cancel();
                break;
            default:
                throw new DomainException($"Unsupported target status transition: '{targetStatus}'.");
        }
    }

    private void IncrementVersion()
    {
        Version++;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static void ValidateInvariants(
        Guid venueId,
        string title,
        string slug,
        string category,
        DateTimeRange salesWindow,
        DateTimeRange schedule,
        int ageRestriction,
        int maxTicketsPerUser)
    {
        if (venueId == Guid.Empty)
        {
            throw new DomainException("Venue ID is required.");
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("Event title cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new DomainException("Event slug cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            throw new DomainException("Event category cannot be empty.");
        }

        if (schedule.Start < salesWindow.Start)
        {
            throw new DomainException("Event start date must be on or after the sales start date.");
        }

        if (ageRestriction < 0)
        {
            throw new DomainException("Age restriction cannot be negative.");
        }

        if (maxTicketsPerUser <= 0)
        {
            throw new DomainException("Maximum tickets per user must be greater than zero.");
        }
    }
}
