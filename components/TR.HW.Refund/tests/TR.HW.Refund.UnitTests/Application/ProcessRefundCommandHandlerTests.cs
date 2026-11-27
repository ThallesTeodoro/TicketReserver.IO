namespace TR.HW.Refund.UnitTests.Application;

using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TR.HW.Refund.Application.Features.ProcessRefund;
using TR.HW.Refund.Application.Interfaces;
using TR.HW.Refund.Domain.Entities;
using TR.HW.Refund.Domain.Enums;
using TR.HW.Refund.Domain.Events;

public sealed class ProcessRefundCommandHandlerTests
{
    private readonly Mock<IRefundRepository> _mockRepository;
    private readonly Mock<IRefundGatewayService> _mockGateway;
    private readonly Mock<IKafkaRefundProducer> _mockKafkaProducer;
    private readonly IValidator<ProcessRefundCommand> _validator;
    private readonly ProcessRefundCommandHandler _handler;

    public ProcessRefundCommandHandlerTests()
    {
        _mockRepository = new Mock<IRefundRepository>();
        _mockGateway = new Mock<IRefundGatewayService>();
        _mockKafkaProducer = new Mock<IKafkaRefundProducer>();
        _validator = new ProcessRefundCommandValidator();

        _handler = new ProcessRefundCommandHandler(
            _mockRepository.Object,
            _mockGateway.Object,
            _mockKafkaProducer.Object,
            _validator,
            NullLogger<ProcessRefundCommandHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_ShouldExecuteRefundAndPublishEvent_WhenCapturedPaymentExists()
    {
        // Arrange
        var reservationId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var command = new ProcessRefundCommand(
            ReservationId: reservationId,
            PaymentId: paymentId,
            CustomerId: customerId,
            Reason: "SEAT_ALREADY_ALLOCATED",
            MessageId: "msg-refund-001",
            Traceparent: "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01");

        var paymentSnapshot = new PaymentSnapshot(
            Id: paymentId,
            ReservationId: reservationId,
            CustomerId: customerId,
            Amount: 220.00m,
            Currency: "BRL",
            Status: PaymentStatus.Captured,
            GatewayProvider: "STRIPE",
            GatewayTransactionId: "ch_test_captured_999",
            Version: 1);

        _mockRepository
            .Setup(r => r.GetPaymentByReservationIdAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(paymentSnapshot);

        _mockGateway
            .Setup(g => g.ProcessRefundAsync(It.IsAny<RefundGatewayRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefundGatewayResult(true, "re_test_success_123", null, "{\"status\":\"succeeded\"}"));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsRefunded.Should().BeTrue();
        result.Status.Should().Be("REFUNDED");
        result.GatewayRefundId.Should().Be("re_test_success_123");
        result.FailureReason.Should().BeNull();

        _mockRepository.Verify(r => r.SaveRefundTransactionallyAsync(
            It.Is<Refund>(refAgg => refAgg.Status == RefundStatus.Completed && refAgg.Amount == 220.00m),
            "msg-refund-001",
            "cg-payment-refund",
            It.IsAny<CancellationToken>()),
            Times.Once);

        _mockKafkaProducer.Verify(p => p.PublishPaymentRefundedAsync(
            It.Is<PaymentRefundedDomainEvent>(e =>
                e.PaymentId == paymentId &&
                e.ReservationId == reservationId &&
                e.GatewayRefundId == "re_test_success_123"),
            command.Traceparent,
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ShouldSkipGatewayAndReturnAlreadyRefunded_WhenPaymentIsAlreadyRefunded()
    {
        // Arrange
        var reservationId = Guid.NewGuid();
        var paymentSnapshot = new PaymentSnapshot(
            Id: Guid.NewGuid(),
            ReservationId: reservationId,
            CustomerId: Guid.NewGuid(),
            Amount: 150.00m,
            Currency: "BRL",
            Status: PaymentStatus.Refunded,
            GatewayProvider: "STRIPE",
            GatewayTransactionId: "ch_test_123",
            Version: 2);

        _mockRepository
            .Setup(r => r.GetPaymentByReservationIdAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(paymentSnapshot);

        var command = new ProcessRefundCommand(
            ReservationId: reservationId,
            PaymentId: paymentSnapshot.Id,
            CustomerId: paymentSnapshot.CustomerId,
            Reason: "SEAT_ALREADY_ALLOCATED",
            MessageId: "msg-dup-refund");

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsRefunded.Should().BeTrue();
        result.Status.Should().Be("ALREADY_REFUNDED");

        _mockGateway.Verify(g => g.ProcessRefundAsync(It.IsAny<RefundGatewayRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockRepository.Verify(r => r.RecordProcessedMessageAsync("msg-dup-refund", "cg-payment-refund", "ticketing.v1.failed", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnPaymentNotFound_WhenNoPaymentExistsForReservation()
    {
        // Arrange
        var reservationId = Guid.NewGuid();
        _mockRepository
            .Setup(r => r.GetPaymentByReservationIdAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentSnapshot?)null);

        var command = new ProcessRefundCommand(
            ReservationId: reservationId,
            PaymentId: null,
            CustomerId: null,
            Reason: "SEAT_ALREADY_ALLOCATED",
            MessageId: "msg-no-payment");

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsRefunded.Should().BeFalse();
        result.Status.Should().Be("PAYMENT_NOT_FOUND");

        _mockGateway.Verify(g => g.ProcessRefundAsync(It.IsAny<RefundGatewayRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockRepository.Verify(r => r.RecordProcessedMessageAsync("msg-no-payment", "cg-payment-refund", "ticketing.v1.failed", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ShouldSaveFailedRefundAndNotPublishEvent_WhenGatewayRejectsRefund()
    {
        // Arrange
        var reservationId = Guid.NewGuid();
        var paymentSnapshot = new PaymentSnapshot(
            Id: Guid.NewGuid(),
            ReservationId: reservationId,
            CustomerId: Guid.NewGuid(),
            Amount: 150.00m,
            Currency: "BRL",
            Status: PaymentStatus.Captured,
            GatewayProvider: "STRIPE",
            GatewayTransactionId: "ch_test_123",
            Version: 1);

        _mockRepository
            .Setup(r => r.GetPaymentByReservationIdAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(paymentSnapshot);

        _mockGateway
            .Setup(g => g.ProcessRefundAsync(It.IsAny<RefundGatewayRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefundGatewayResult(false, null, "GATEWAY_ERROR", "{\"error\":\"failed\"}"));

        var command = new ProcessRefundCommand(
            ReservationId: reservationId,
            PaymentId: paymentSnapshot.Id,
            CustomerId: paymentSnapshot.CustomerId,
            Reason: "SEAT_ALREADY_ALLOCATED",
            MessageId: "msg-failed-gw");

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsRefunded.Should().BeFalse();
        result.Status.Should().Be("FAILED");
        result.FailureReason.Should().Be("GATEWAY_ERROR");

        _mockRepository.Verify(r => r.SaveRefundTransactionallyAsync(
            It.Is<Refund>(refAgg => refAgg.Status == RefundStatus.Failed),
            "msg-failed-gw",
            "cg-payment-refund",
            It.IsAny<CancellationToken>()),
            Times.Once);

        _mockKafkaProducer.Verify(p => p.PublishPaymentRefundedAsync(
            It.IsAny<PaymentRefundedDomainEvent>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
