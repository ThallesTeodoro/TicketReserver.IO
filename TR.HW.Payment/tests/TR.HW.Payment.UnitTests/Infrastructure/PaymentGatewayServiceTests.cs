namespace TR.HW.Payment.UnitTests.Infrastructure;

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TR.HW.Payment.Application.Interfaces;
using TR.HW.Payment.Infrastructure.Configuration;
using TR.HW.Payment.Infrastructure.Gateways;

public sealed class PaymentGatewayServiceTests
{
    private readonly PaymentGatewayOptions _options;

    public PaymentGatewayServiceTests()
    {
        _options = new PaymentGatewayOptions
        {
            Provider = "STRIPE",
            SimulateSuccess = true,
            MaxRetries = 2
        };
    }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldReturnSuccess_WhenSimulateSuccessIsTrue()
    {
        // Arrange
        var service = new PaymentGatewayService(
            Options.Create(_options),
            NullLogger<PaymentGatewayService>.Instance);

        var request = new PaymentGatewayRequest(
            ReservationId: Guid.NewGuid(),
            CustomerId: Guid.NewGuid(),
            Amount: 150.00m,
            Currency: "BRL",
            PaymentMethod: "CREDIT_CARD");

        // Act
        var result = await service.ProcessPaymentAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.GatewayTransactionId.Should().StartWith("ch_");
        result.AuthorizationCode.Should().StartWith("AUTH_");
        result.FailureReason.Should().BeNull();
        result.RawResponse.Should().Contain("succeeded");
    }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldReturnDecline_WhenAmountTriggersDecline()
    {
        // Arrange
        var service = new PaymentGatewayService(
            Options.Create(_options),
            NullLogger<PaymentGatewayService>.Instance);

        var request = new PaymentGatewayRequest(
            ReservationId: Guid.NewGuid(),
            CustomerId: Guid.NewGuid(),
            Amount: 999.99m,
            Currency: "BRL",
            PaymentMethod: "CREDIT_CARD");

        // Act
        var result = await service.ProcessPaymentAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.FailureReason.Should().Be("INSUFFICIENT_FUNDS");
        result.AuthorizationCode.Should().BeNull();
    }

    [Fact]
    public async Task ProcessPaymentAsync_ShouldRetryAndThrow_WhenTimeoutOccursConsistently()
    {
        // Arrange
        var service = new PaymentGatewayService(
            Options.Create(_options),
            NullLogger<PaymentGatewayService>.Instance);

        var request = new PaymentGatewayRequest(
            ReservationId: Guid.NewGuid(),
            CustomerId: Guid.NewGuid(),
            Amount: 888.88m, // triggers simulated TimeoutException
            Currency: "BRL",
            PaymentMethod: "CREDIT_CARD");

        // Act & Assert
        var act = async () => await service.ProcessPaymentAsync(request);
        await act.Should().ThrowAsync<TimeoutException>();
    }
}
