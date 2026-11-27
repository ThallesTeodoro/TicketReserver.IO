namespace TR.HW.Payment.UnitTests.Application;

using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Moq;
using TR.HW.Payment.Application.Features.ProcessPayment;
using TR.HW.Payment.Application.Interfaces;
using TR.HW.Payment.Domain.Entities;
using TR.HW.Payment.Domain.Enums;
using TR.HW.Payment.Domain.Events;

public class ProcessPaymentCommandHandlerTests
{
    private readonly Mock<IPaymentRepository> _repoMock = new();
    private readonly Mock<IPaymentGatewayService> _gatewayMock = new();
    private readonly Mock<IKafkaPaymentProducer> _kafkaMock = new();
    private readonly Mock<ILogger<ProcessPaymentCommandHandler>> _loggerMock = new();
    private readonly ProcessPaymentCommandValidator _validator = new();
    private readonly ProcessPaymentCommandHandler _sut;

    public ProcessPaymentCommandHandlerTests()
    {
        _sut = new ProcessPaymentCommandHandler(
            _repoMock.Object,
            _gatewayMock.Object,
            _kafkaMock.Object,
            _validator,
            _loggerMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WhenValidationFails_ShouldThrowValidationException()
    {
        var command = new ProcessPaymentCommand(
            Guid.Empty,
            Guid.Empty,
            0,
            "INVALID_CURRENCY",
            "CREDIT_CARD");

        var act = () => _sut.HandleAsync(command);

        await act.Should().ThrowAsync<ValidationException>();
        _gatewayMock.Verify(g => g.ProcessPaymentAsync(It.IsAny<PaymentGatewayRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenPaymentAlreadyExists_ShouldSkipGatewayAndReturnAlreadyProcessed()
    {
        var reservationId = Guid.NewGuid();
        var command = new ProcessPaymentCommand(
            reservationId,
            Guid.NewGuid(),
            100m,
            "BRL",
            "CREDIT_CARD",
            MessageId: "msg-123");

        _repoMock.Setup(r => r.HasExistingPaymentForReservationAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.HandleAsync(command);

        result.Status.Should().Be("ALREADY_PROCESSED");
        _gatewayMock.Verify(g => g.ProcessPaymentAsync(It.IsAny<PaymentGatewayRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _repoMock.Verify(r => r.RecordProcessedMessageAsync("msg-123", "cg-payment-processing", "reservation.v1.checkout_req", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenGatewayApproves_ShouldCapturePaymentAndPublishApprovedEvent()
    {
        var reservationId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var command = new ProcessPaymentCommand(
            reservationId,
            customerId,
            250.00m,
            "BRL",
            "CREDIT_CARD",
            CardLastFour: "1234",
            CardBrand: "Mastercard",
            MessageId: "msg-approved",
            Traceparent: "00-trace-01");

        _repoMock.Setup(r => r.HasExistingPaymentForReservationAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _gatewayMock.Setup(g => g.ProcessPaymentAsync(It.IsAny<PaymentGatewayRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentGatewayResult(true, "ch_stripe_999", "AUTH_OK", null, "{\"status\":\"succeeded\"}"));

        var result = await _sut.HandleAsync(command);

        result.IsApproved.Should().BeTrue();
        result.Status.Should().Be("Captured");
        result.GatewayTransactionId.Should().Be("ch_stripe_999");

        _repoMock.Verify(r => r.SavePaymentTransactionallyAsync(
            It.Is<Payment>(p => p.Status == PaymentStatus.Captured && p.Amount == 250m),
            "msg-approved",
            "cg-payment-processing",
            It.IsAny<CancellationToken>()), Times.Once);

        _kafkaMock.Verify(k => k.PublishPaymentApprovedAsync(
            It.Is<PaymentApprovedDomainEvent>(e => e.ReservationId == reservationId && e.GatewayTransactionId == "ch_stripe_999"),
            "00-trace-01",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenGatewayDeclines_ShouldDeclinePaymentAndPublishDeclinedEvent()
    {
        var reservationId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var command = new ProcessPaymentCommand(
            reservationId,
            customerId,
            120.00m,
            "BRL",
            "CREDIT_CARD",
            MessageId: "msg-declined");

        _repoMock.Setup(r => r.HasExistingPaymentForReservationAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _gatewayMock.Setup(g => g.ProcessPaymentAsync(It.IsAny<PaymentGatewayRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentGatewayResult(false, null, null, "Insufficient funds", "{\"status\":\"failed\"}"));

        var result = await _sut.HandleAsync(command);

        result.IsApproved.Should().BeFalse();
        result.Status.Should().Be("Declined");
        result.FailureReason.Should().Be("Insufficient funds");

        _repoMock.Verify(r => r.SavePaymentTransactionallyAsync(
            It.Is<Payment>(p => p.Status == PaymentStatus.Declined),
            "msg-declined",
            "cg-payment-processing",
            It.IsAny<CancellationToken>()), Times.Once);

        _kafkaMock.Verify(k => k.PublishPaymentDeclinedAsync(
            It.Is<PaymentDeclinedDomainEvent>(e => e.ReservationId == reservationId && e.FailureReason == "Insufficient funds"),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
