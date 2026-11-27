namespace TR.HW.Refund.UnitTests.Infrastructure;

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TR.HW.Refund.Application.Interfaces;
using TR.HW.Refund.Infrastructure.Configuration;
using TR.HW.Refund.Infrastructure.Gateways;

public sealed class RefundGatewayServiceTests
{
    private readonly RefundGatewayOptions _options;

    public RefundGatewayServiceTests()
    {
        _options = new RefundGatewayOptions
        {
            Provider = "STRIPE",
            SimulateSuccess = true,
            MaxRetries = 2
        };
    }

    [Fact]
    public async Task ProcessRefundAsync_ShouldReturnSuccess_WhenSimulateSuccessIsTrue()
    {
        // Arrange
        var service = new RefundGatewayService(
            Options.Create(_options),
            NullLogger<RefundGatewayService>.Instance);

        var request = new RefundGatewayRequest(
            PaymentId: Guid.NewGuid(),
            ReservationId: Guid.NewGuid(),
            GatewayTransactionId: "ch_test_12345",
            Amount: 180.00m,
            Currency: "BRL",
            Reason: "SEAT_ALREADY_ALLOCATED");

        // Act
        var result = await service.ProcessRefundAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.GatewayRefundId.Should().StartWith("re_");
        result.FailureReason.Should().BeNull();
        result.RawResponse.Should().Contain("succeeded");
    }

    [Fact]
    public async Task ProcessRefundAsync_ShouldReturnFailure_WhenAmountTriggersRejection()
    {
        // Arrange
        var service = new RefundGatewayService(
            Options.Create(_options),
            NullLogger<RefundGatewayService>.Instance);

        var request = new RefundGatewayRequest(
            PaymentId: Guid.NewGuid(),
            ReservationId: Guid.NewGuid(),
            GatewayTransactionId: "ch_test_12345",
            Amount: 999.99m,
            Currency: "BRL",
            Reason: "SEAT_ALREADY_ALLOCATED");

        // Act
        var result = await service.ProcessRefundAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.FailureReason.Should().Be("CHARGE_ALREADY_DISPUTED");
        result.GatewayRefundId.Should().BeNull();
    }

    [Fact]
    public async Task ProcessRefundAsync_ShouldRetryAndThrow_WhenTimeoutOccursConsistently()
    {
        // Arrange
        var service = new RefundGatewayService(
            Options.Create(_options),
            NullLogger<RefundGatewayService>.Instance);

        var request = new RefundGatewayRequest(
            PaymentId: Guid.NewGuid(),
            ReservationId: Guid.NewGuid(),
            GatewayTransactionId: "ch_test_12345",
            Amount: 888.88m, // triggers simulated TimeoutException
            Currency: "BRL",
            Reason: "SEAT_ALREADY_ALLOCATED");

        // Act & Assert
        var act = async () => await service.ProcessRefundAsync(request);
        await act.Should().ThrowAsync<TimeoutException>();
    }
}
