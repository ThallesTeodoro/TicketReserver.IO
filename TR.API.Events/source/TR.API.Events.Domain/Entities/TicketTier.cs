namespace TR.API.Events.Domain.Entities;

using TR.API.Events.Domain.Enums;
using TR.API.Events.Domain.Exceptions;
using TR.API.Events.Domain.ValueObjects;

public class TicketTier
{
    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public Guid SectionId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public TicketTierType TierType { get; private set; } = TicketTierType.STANDARD;
    public decimal Price { get; private set; }
    public string Currency { get; private set; } = "BRL";
    public int TotalQuantity { get; private set; }
    public DateTimeOffset SalesStartAt { get; private set; }
    public DateTimeOffset SalesEndAt { get; private set; }
    public TicketTierStatus Status { get; private set; } = TicketTierStatus.SCHEDULED;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private TicketTier() { }

    public static TicketTier Create(
        Guid eventId,
        Guid sectionId,
        string name,
        TicketTierType tierType,
        Money price,
        int totalQuantity,
        DateTimeRange salesWindow)
    {
        if (eventId == Guid.Empty)
        {
            throw new DomainException("Event ID cannot be empty.");
        }

        if (sectionId == Guid.Empty)
        {
            throw new DomainException("Section ID cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Ticket tier name cannot be empty.");
        }

        if (totalQuantity <= 0)
        {
            throw new DomainException("Ticket tier total quantity must be greater than zero.");
        }

        var now = DateTimeOffset.UtcNow;

        return new TicketTier
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            SectionId = sectionId,
            Name = name.Trim(),
            TierType = tierType,
            Price = price.Amount,
            Currency = price.Currency,
            TotalQuantity = totalQuantity,
            SalesStartAt = salesWindow.Start,
            SalesEndAt = salesWindow.End,
            Status = TicketTierStatus.SCHEDULED,
            CreatedAt = now,
            UpdatedAt = now
        };
    }
}
