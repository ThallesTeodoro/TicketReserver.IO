namespace TR.API.Events.Domain.ValueObjects;

using TR.API.Events.Domain.Exceptions;

public sealed record DateTimeRange
{
    public DateTimeOffset Start { get; }
    public DateTimeOffset End { get; }

    public DateTimeRange(DateTimeOffset start, DateTimeOffset end, string rangeName = "Date range")
    {
        if (end <= start)
        {
            throw new DomainException($"{rangeName} end date ({end:O}) must be strictly greater than start date ({start:O}).");
        }

        Start = start;
        End = end;
    }
}
