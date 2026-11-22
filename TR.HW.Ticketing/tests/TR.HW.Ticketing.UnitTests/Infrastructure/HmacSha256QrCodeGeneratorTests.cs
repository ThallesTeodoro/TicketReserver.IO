namespace TR.HW.Ticketing.UnitTests.Infrastructure;

using FluentAssertions;
using Microsoft.Extensions.Options;
using TR.HW.Ticketing.Infrastructure.Configuration;
using TR.HW.Ticketing.Infrastructure.Cryptography;

public sealed class HmacSha256QrCodeGeneratorTests
{
    private readonly HmacSha256QrCodeGenerator _generator;

    public HmacSha256QrCodeGeneratorTests()
    {
        var options = Options.Create(new TicketingSecurityOptions
        {
            QrCodeSecretKey = "test-secret-key-for-unit-testing-hmac-2026"
        });
        _generator = new HmacSha256QrCodeGenerator(options);
    }

    [Fact]
    public void GenerateSignature_ShouldReturnConsistentUrlSafeBase64Hash()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var issuedAt = new DateTime(2026, 11, 21, 10, 0, 0, DateTimeKind.Utc);

        // Act
        var signature1 = _generator.GenerateSignature(ticketId, eventId, customerId, issuedAt);
        var signature2 = _generator.GenerateSignature(ticketId, eventId, customerId, issuedAt);

        // Assert
        signature1.Should().NotBeNullOrWhiteSpace();
        signature1.Should().Be(signature2);
        signature1.Should().NotContain("+");
        signature1.Should().NotContain("/");
        signature1.Should().NotContain("=");
    }

    [Fact]
    public void VerifySignature_ShouldReturnTrue_WhenParametersMatch()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var issuedAt = DateTime.UtcNow;

        var signature = _generator.GenerateSignature(ticketId, eventId, customerId, issuedAt);

        // Act
        var isValid = _generator.VerifySignature(ticketId, eventId, customerId, issuedAt, signature);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void VerifySignature_ShouldReturnFalse_WhenParametersAreTampered()
    {
        // Arrange
        var ticketId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var issuedAt = DateTime.UtcNow;

        var signature = _generator.GenerateSignature(ticketId, eventId, customerId, issuedAt);

        // Act
        var isValid = _generator.VerifySignature(Guid.NewGuid(), eventId, customerId, issuedAt, signature);

        // Assert
        isValid.Should().BeFalse();
    }
}
