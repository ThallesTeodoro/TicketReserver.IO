namespace TR.API.Events.Domain.ValueObjects;

using TR.API.Events.Domain.Exceptions;

public sealed record Money
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency = "BRL")
    {
        if (amount < 0)
        {
            throw new DomainException("Monetary amount cannot be negative.");
        }

        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
        {
            throw new DomainException("Currency must be a 3-letter ISO code.");
        }

        Amount = amount;
        Currency = currency.Trim().ToUpperInvariant();
    }
}
