namespace TR.HW.Ticketing.Domain.Services;

public interface IQrCodeGenerator
{
    string GenerateSignature(Guid ticketId, Guid eventId, Guid customerId, DateTime issuedAt);
    bool VerifySignature(Guid ticketId, Guid eventId, Guid customerId, DateTime issuedAt, string signature);
}
