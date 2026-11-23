using TR.HW.Repl.Domain.Exceptions;
using TR.HW.Repl.Domain.ValueObjects;

namespace TR.HW.Repl.Domain.Entities;

public sealed class EventSearchDocument
{
    private readonly List<TicketTierSummary> _tiersSummary = [];
    private readonly List<string> _tags = [];

    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public string Status { get; private set; } = "DRAFT";
    public int AgeRestriction { get; private set; }
    public string? BannerUrl { get; private set; }
    public EventScheduleInfo Schedule { get; private set; } = null!;
    public VenueSearchInfo Venue { get; private set; } = null!;
    public PricingSummary Pricing { get; private set; } = new(0, 0, "BRL");
    public IReadOnlyCollection<TicketTierSummary> TiersSummary => _tiersSummary.AsReadOnly();
    public IReadOnlyCollection<string> Tags => _tags.AsReadOnly();
    public DateTimeOffset UpdatedAt { get; private set; }

    private EventSearchDocument()
    {
    }

    public static EventSearchDocument Create(
        Guid id,
        string title,
        string slug,
        string description,
        string category,
        string status,
        int ageRestriction,
        string? bannerUrl,
        EventScheduleInfo schedule,
        VenueSearchInfo venue,
        IEnumerable<string>? tags = null,
        IEnumerable<TicketTierSummary>? initialTiers = null,
        DateTimeOffset? updatedAt = null)
    {
        if (id == Guid.Empty)
            throw new DomainException("Event identifier cannot be empty.");

        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("Event title cannot be empty.");

        if (string.IsNullOrWhiteSpace(slug))
            throw new DomainException("Event slug cannot be empty.");

        if (string.IsNullOrWhiteSpace(category))
            throw new DomainException("Event category cannot be empty.");

        if (ageRestriction < 0)
            throw new DomainException("Age restriction cannot be negative.");

        var doc = new EventSearchDocument
        {
            Id = id,
            Title = title.Trim(),
            Slug = slug.Trim(),
            Description = description?.Trim() ?? string.Empty,
            Category = category.Trim().ToUpperInvariant(),
            Status = string.IsNullOrWhiteSpace(status) ? "DRAFT" : status.Trim().ToUpperInvariant(),
            AgeRestriction = ageRestriction,
            BannerUrl = string.IsNullOrWhiteSpace(bannerUrl) ? null : bannerUrl.Trim(),
            Schedule = schedule ?? throw new DomainException("Schedule must be specified."),
            Venue = venue ?? throw new DomainException("Venue must be specified."),
            UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow
        };

        if (tags != null)
        {
            foreach (var tag in tags)
            {
                if (!string.IsNullOrWhiteSpace(tag))
                    doc._tags.Add(tag.Trim().ToLowerInvariant());
            }
        }

        if (initialTiers != null)
        {
            foreach (var tier in initialTiers)
            {
                doc.UpsertTier(tier);
            }
        }
        else
        {
            doc.RecalculatePricing();
        }

        return doc;
    }

    public void UpdateEventDetails(
        string title,
        string slug,
        string description,
        string category,
        string status,
        int ageRestriction,
        string? bannerUrl,
        EventScheduleInfo schedule,
        IEnumerable<string>? tags = null,
        DateTimeOffset? updatedAt = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("Event title cannot be empty.");

        if (string.IsNullOrWhiteSpace(slug))
            throw new DomainException("Event slug cannot be empty.");

        Title = title.Trim();
        Slug = slug.Trim();
        Description = description?.Trim() ?? string.Empty;
        Category = category.Trim().ToUpperInvariant();
        Status = status.Trim().ToUpperInvariant();
        AgeRestriction = ageRestriction;
        BannerUrl = string.IsNullOrWhiteSpace(bannerUrl) ? null : bannerUrl.Trim();
        Schedule = schedule ?? throw new DomainException("Schedule must be specified.");

        if (tags != null)
        {
            _tags.Clear();
            foreach (var tag in tags)
            {
                if (!string.IsNullOrWhiteSpace(tag))
                    _tags.Add(tag.Trim().ToLowerInvariant());
            }
        }

        UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow;
    }

    public void UpdateVenue(VenueSearchInfo venue, DateTimeOffset? updatedAt = null)
    {
        Venue = venue ?? throw new DomainException("Venue must be specified.");
        UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow;
    }

    public void UpsertTier(TicketTierSummary tier, DateTimeOffset? updatedAt = null)
    {
        if (tier == null)
            throw new DomainException("Tier cannot be null.");

        var existingIndex = _tiersSummary.FindIndex(t => t.TierId == tier.TierId);
        if (existingIndex >= 0)
        {
            _tiersSummary[existingIndex] = tier;
        }
        else
        {
            _tiersSummary.Add(tier);
        }

        RecalculatePricing();
        UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow;
    }

    public void RemoveTier(Guid tierId, DateTimeOffset? updatedAt = null)
    {
        _tiersSummary.RemoveAll(t => t.TierId == tierId);
        RecalculatePricing();
        UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow;
    }

    public void MarkAsCancelled(DateTimeOffset? updatedAt = null)
    {
        Status = "CANCELLED";
        UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow;
    }

    private void RecalculatePricing()
    {
        var activeTiers = _tiersSummary.Where(t => t.Status == "ACTIVE").ToList();
        if (activeTiers.Count == 0)
        {
            Pricing = new PricingSummary(0, 0, "BRL");
            return;
        }

        var min = activeTiers.Min(t => t.Price);
        var max = activeTiers.Max(t => t.Price);
        Pricing = new PricingSummary(min, max, "BRL");
    }
}
