using TR.HW.Repl.Domain.Exceptions;

namespace TR.HW.Repl.Domain.Entities;

public sealed class TicketTierSummary
{
    public Guid TierId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public string Status { get; private set; } = "ACTIVE";

    private TicketTierSummary()
    {
    }

    public static TicketTierSummary Create(Guid tierId, string name, decimal price, string status = "ACTIVE")
    {
        if (tierId == Guid.Empty)
            throw new DomainException("TierId cannot be empty.");

        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Tier name cannot be empty.");

        if (price < 0)
            throw new DomainException("Tier price cannot be negative.");

        return new TicketTierSummary
        {
            TierId = tierId,
            Name = name.Trim(),
            Price = price,
            Status = string.IsNullOrWhiteSpace(status) ? "ACTIVE" : status.Trim()
        };
    }
}
