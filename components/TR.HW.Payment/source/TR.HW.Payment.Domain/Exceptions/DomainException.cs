namespace TR.HW.Payment.Domain.Exceptions;

public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
    public DomainException(string message, Exception innerException) : base(message, innerException) { }
}

public sealed class PaymentDomainException : DomainException
{
    public PaymentDomainException(string message) : base(message) { }
}
