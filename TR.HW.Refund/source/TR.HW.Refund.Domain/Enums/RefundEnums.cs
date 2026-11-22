namespace TR.HW.Refund.Domain.Enums;

public enum RefundStatus
{
    Pending = 1,
    Completed = 2,
    Failed = 3
}

public enum PaymentStatus
{
    Pending = 1,
    Authorized = 2,
    Captured = 3,
    Declined = 4,
    Refunded = 5,
    PartiallyRefunded = 6,
    Failed = 7
}

public enum TransactionType
{
    Authorize = 1,
    Capture = 2,
    Void = 3,
    Refund = 4
}

public enum TransactionStatus
{
    Success = 1,
    Failed = 2
}
