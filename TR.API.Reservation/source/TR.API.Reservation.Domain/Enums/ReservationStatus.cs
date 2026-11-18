namespace TR.API.Reservation.Domain.Enums;

public enum ReservationStatus
{
    AWAITING_PAYMENT,
    PROCESSING_PAYMENT,
    CONFIRMED,
    EXPIRED,
    CANCELLED
}
