namespace TR.HW.ResCompensator.Domain.Enums;

public enum ReservationStatus
{
    AwaitingPayment = 1,
    ProcessingPayment = 2,
    Confirmed = 3,
    Expired = 4,
    Cancelled = 5
}

public enum ReservationOutcomeType
{
    Confirmed = 1,
    CancelledByRefund = 2,
    CancelledByDecline = 3
}
