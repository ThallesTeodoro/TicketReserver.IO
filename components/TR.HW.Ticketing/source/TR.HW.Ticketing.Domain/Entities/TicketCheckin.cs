namespace TR.HW.Ticketing.Domain.Entities;

using TR.HW.Ticketing.Domain.Enums;
using TR.HW.Ticketing.Domain.Exceptions;

public sealed class TicketCheckin
{
    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public string GateName { get; private set; } = string.Empty;
    public string DeviceId { get; private set; } = string.Empty;
    public CheckinStatus Status { get; private set; }
    public string? Details { get; private set; }
    public DateTime ValidatedAt { get; private set; }

    private TicketCheckin() { }

    public static TicketCheckin Create(
        Guid ticketId,
        string gateName,
        string deviceId,
        CheckinStatus status,
        string? details = null)
    {
        if (ticketId == Guid.Empty)
        {
            throw new DomainException("TicketId cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(gateName))
        {
            throw new DomainException("GateName cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(deviceId))
        {
            throw new DomainException("DeviceId cannot be empty.");
        }

        return new TicketCheckin
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            GateName = gateName.Trim(),
            DeviceId = deviceId.Trim(),
            Status = status,
            Details = details,
            ValidatedAt = DateTime.UtcNow
        };
    }
}
