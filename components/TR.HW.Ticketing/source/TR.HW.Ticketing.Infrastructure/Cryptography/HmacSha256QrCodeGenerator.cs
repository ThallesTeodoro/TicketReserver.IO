namespace TR.HW.Ticketing.Infrastructure.Cryptography;

using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using TR.HW.Ticketing.Domain.Services;
using TR.HW.Ticketing.Infrastructure.Configuration;

public sealed class HmacSha256QrCodeGenerator : IQrCodeGenerator
{
    private readonly byte[] _secretKeyBytes;

    public HmacSha256QrCodeGenerator(IOptions<TicketingSecurityOptions> options)
    {
        var secret = options.Value.QrCodeSecretKey;
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new ArgumentException("QrCodeSecretKey cannot be empty.", nameof(options));
        }

        _secretKeyBytes = Encoding.UTF8.GetBytes(secret);
    }

    public string GenerateSignature(Guid ticketId, Guid eventId, Guid customerId, DateTime issuedAt)
    {
        var unixTimestamp = new DateTimeOffset(issuedAt.ToUniversalTime()).ToUnixTimeSeconds();
        var payload = $"{ticketId:N}:{eventId:N}:{customerId:N}:{unixTimestamp}";
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        var hashBytes = HMACSHA256.HashData(_secretKeyBytes, payloadBytes);

        return Convert.ToBase64String(hashBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public bool VerifySignature(Guid ticketId, Guid eventId, Guid customerId, DateTime issuedAt, string signature)
    {
        if (string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        var expectedSignature = GenerateSignature(ticketId, eventId, customerId, issuedAt);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedSignature);
        var actualBytes = Encoding.UTF8.GetBytes(signature);

        return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }
}
