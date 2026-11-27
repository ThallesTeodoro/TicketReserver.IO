namespace TR.HW.ResCompensator.UnitTests.Application;

using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TR.HW.ResCompensator.Application.Features.ReconcileReservation;
using TR.HW.ResCompensator.Application.Interfaces;
using TR.HW.ResCompensator.Domain.Entities;
using TR.HW.ResCompensator.Domain.Enums;

public sealed class ReconcileReservationOutcomeCommandHandlerTests
{
    private readonly Mock<IReservationCompensationRepository> _mockRepository;
    private readonly Mock<IRedisSeatLockReleaser> _mockSeatReleaser;
    private readonly IValidator<ReconcileReservationOutcomeCommand> _validator;
    private readonly ReconcileReservationOutcomeCommandHandler _handler;

    public ReconcileReservationOutcomeCommandHandlerTests()
    {
        _mockRepository = new Mock<IReservationCompensationRepository>();
        _mockSeatReleaser = new Mock<IRedisSeatLockReleaser>();
        _validator = new ReconcileReservationOutcomeCommandValidator();

        _handler = new ReconcileReservationOutcomeCommandHandler(
            _mockRepository.Object,
            _mockSeatReleaser.Object,
            _validator,
            NullLogger<ReconcileReservationOutcomeCommandHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_ShouldConfirmReservation_WhenOutcomeIsConfirmed()
    {
        // Arrange
        var reservationId = Guid.NewGuid();
        var reservation = ReservationState.Reconstitute(
            id: reservationId,
            customerId: Guid.NewGuid(),
            eventId: Guid.NewGuid(),
            status: ReservationStatus.ProcessingPayment,
            totalAmount: 150.00m,
            currency: "BRL",
            version: 1);

        _mockRepository
            .Setup(r => r.GetReservationByIdAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);

        var command = new ReconcileReservationOutcomeCommand(
            ReservationId: reservationId,
            OutcomeType: ReservationOutcomeType.Confirmed,
            EventType: "ticketing.v1.issued",
            MessageId: "msg-confirm-001");

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsReconciled.Should().BeTrue();
        result.FinalStatus.Should().Be("CONFIRMED");
        result.ReleasedRedisSeatsCount.Should().Be(0);

        _mockRepository.Verify(r => r.SaveReservationStateTransactionallyAsync(
            It.Is<ReservationState>(res => res.Status == ReservationStatus.Confirmed && res.Version == 2),
            "msg-confirm-001",
            "cg-reservation-compensator",
            "ticketing.v1.issued",
            It.IsAny<CancellationToken>()),
            Times.Once);

        _mockSeatReleaser.Verify(s => s.ReleaseHeldSeatsAsync(
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<IEnumerable<Guid>>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ShouldCancelReservationAndReleaseRedisSeatLocks_WhenOutcomeIsCancelledByRefund()
    {
        // Arrange
        var reservationId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var seatId1 = Guid.NewGuid();
        var seatId2 = Guid.NewGuid();

        var items = new[]
        {
            ReservedSeatItem.Create(Guid.NewGuid(), reservationId, Guid.NewGuid(), seatId1, "A1"),
            ReservedSeatItem.Create(Guid.NewGuid(), reservationId, Guid.NewGuid(), seatId2, "A2")
        };

        var reservation = ReservationState.Reconstitute(
            id: reservationId,
            customerId: Guid.NewGuid(),
            eventId: eventId,
            status: ReservationStatus.ProcessingPayment,
            totalAmount: 300.00m,
            currency: "BRL",
            version: 1,
            items: items);

        _mockRepository
            .Setup(r => r.GetReservationByIdAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);

        _mockSeatReleaser
            .Setup(s => s.ReleaseHeldSeatsAsync(eventId, reservationId, It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var command = new ReconcileReservationOutcomeCommand(
            ReservationId: reservationId,
            OutcomeType: ReservationOutcomeType.CancelledByRefund,
            EventType: "payment.v1.refunded",
            Reason: "AUTO_REFUND_SEAT_COLLISION",
            MessageId: "msg-refund-cancel-001");

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsReconciled.Should().BeTrue();
        result.FinalStatus.Should().Be("CANCELLED");
        result.CancellationReason.Should().Be("AUTO_REFUND_SEAT_COLLISION");
        result.ReleasedRedisSeatsCount.Should().Be(2);

        _mockRepository.Verify(r => r.SaveReservationStateTransactionallyAsync(
            It.Is<ReservationState>(res =>
                res.Status == ReservationStatus.Cancelled &&
                res.CancellationReason == "AUTO_REFUND_SEAT_COLLISION"),
            "msg-refund-cancel-001",
            "cg-reservation-compensator",
            "payment.v1.refunded",
            It.IsAny<CancellationToken>()),
            Times.Once);

        _mockSeatReleaser.Verify(s => s.ReleaseHeldSeatsAsync(
            eventId,
            reservationId,
            It.Is<IEnumerable<Guid>>(seats => seats.Count() == 2),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ShouldCancelReservationWithPaymentDeclinedReason_WhenOutcomeIsCancelledByDecline()
    {
        // Arrange
        var reservationId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var seatId = Guid.NewGuid();

        var reservation = ReservationState.Reconstitute(
            id: reservationId,
            customerId: Guid.NewGuid(),
            eventId: eventId,
            status: ReservationStatus.ProcessingPayment,
            totalAmount: 150.00m,
            currency: "BRL",
            version: 1,
            items: new[] { ReservedSeatItem.Create(Guid.NewGuid(), reservationId, Guid.NewGuid(), seatId, "B10") });

        _mockRepository
            .Setup(r => r.GetReservationByIdAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);

        _mockSeatReleaser
            .Setup(s => s.ReleaseHeldSeatsAsync(eventId, reservationId, It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = new ReconcileReservationOutcomeCommand(
            ReservationId: reservationId,
            OutcomeType: ReservationOutcomeType.CancelledByDecline,
            EventType: "payment.v1.declined",
            Reason: null,
            MessageId: "msg-decline-001");

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsReconciled.Should().BeTrue();
        result.FinalStatus.Should().Be("CANCELLED");
        result.CancellationReason.Should().Be("PAYMENT_DECLINED");
        result.ReleasedRedisSeatsCount.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnNotFoundAndRecordInbox_WhenReservationDoesNotExist()
    {
        // Arrange
        var reservationId = Guid.NewGuid();
        _mockRepository
            .Setup(r => r.GetReservationByIdAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReservationState?)null);

        var command = new ReconcileReservationOutcomeCommand(
            ReservationId: reservationId,
            OutcomeType: ReservationOutcomeType.Confirmed,
            EventType: "ticketing.v1.issued",
            MessageId: "msg-not-found");

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsReconciled.Should().BeFalse();
        result.FinalStatus.Should().Be("NOT_FOUND");

        _mockRepository.Verify(r => r.RecordProcessedMessageAsync(
            "msg-not-found",
            "cg-reservation-compensator",
            "ticketing.v1.issued",
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
