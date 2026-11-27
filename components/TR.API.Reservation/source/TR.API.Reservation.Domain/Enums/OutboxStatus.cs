namespace TR.API.Reservation.Domain.Enums;

public enum OutboxStatus
{
    PENDING,
    PROCESSING,
    PUBLISHED,
    FAILED
}
