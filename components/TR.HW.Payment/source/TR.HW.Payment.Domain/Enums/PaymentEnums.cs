namespace TR.HW.Payment.Domain.Enums;

public enum PaymentStatus
{
    Pending,
    Authorized,
    Captured,
    Declined,
    Refunded,
    PartiallyRefunded,
    Failed
}

public enum PaymentMethod
{
    CreditCard,
    DebitCard,
    Pix
}

public enum TransactionType
{
    Authorize,
    Capture,
    Void,
    Refund
}

public enum TransactionStatus
{
    Success,
    Failed
}
