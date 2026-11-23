namespace TR.HW.ResCompensator.UnitTests.Domain;

using FluentAssertions;
using TR.HW.ResCompensator.Domain.Entities;
using TR.HW.ResCompensator.Domain.Enums;
using TR.HW.ResCompensator.Domain.Exceptions;

public sealed class ReservationDomainTests
{
    [Fact]
    public void Confirm_ShouldTransitionToConfirmedAndIncrementVersion_WhenProcessingPayment()
    {
        // Arrange
        var reservation = ReservationState.Reconstitute(
            id: Guid.NewGuid(),
            customerId: Guid.NewGuid(),
            eventId: Guid.NewGuid(),
            status: ReservationStatus.ProcessingPayment,
            totalAmount: 180.00m,
            currency: "BRL",
            version: 1);

        // Act
        var changed = reservation.Confirm();

        // Assert
        changed.Should().BeTrue();
        reservation.Status.Should().Be(ReservationStatus.Confirmed);
        reservation.ConfirmedAt.Should().NotBeNull();
        reservation.Version.Should().Be(2);
    }

    [Fact]
    public void Confirm_ShouldReturnFalseAndNotIncrementVersion_WhenAlreadyConfirmed()
    {
        // Arrange
        var reservation = ReservationState.Reconstitute(
            id: Guid.NewGuid(),
            customerId: Guid.NewGuid(),
            eventId: Guid.NewGuid(),
            status: ReservationStatus.Confirmed,
            totalAmount: 180.00m,
            currency: "BRL",
            version: 2,
            confirmedAt: DateTime.UtcNow);

        // Act
        var changed = reservation.Confirm();

        // Assert
        changed.Should().BeFalse();
        reservation.Status.Should().Be(ReservationStatus.Confirmed);
        reservation.Version.Should().Be(2);
    }

    [Fact]
    public void Confirm_ShouldThrowDomainException_WhenAlreadyCancelled()
    {
        // Arrange
        var reservation = ReservationState.Reconstitute(
            id: Guid.NewGuid(),
            customerId: Guid.NewGuid(),
            eventId: Guid.NewGuid(),
            status: ReservationStatus.Cancelled,
            totalAmount: 180.00m,
            currency: "BRL",
            version: 2);

        // Act
        var act = () => reservation.Confirm();

        // Assert
        act.Should().Throw<DomainException>()
            .WithMessage("*Cannot confirm reservation in terminal status*");
    }

    [Fact]
    public void Cancel_ShouldTransitionToCancelledAndSetReason_WhenProcessingPayment()
    {
        // Arrange
        var reservation = ReservationState.Reconstitute(
            id: Guid.NewGuid(),
            customerId: Guid.NewGuid(),
            eventId: Guid.NewGuid(),
            status: ReservationStatus.ProcessingPayment,
            totalAmount: 180.00m,
            currency: "BRL",
            version: 1);

        // Act
        var changed = reservation.Cancel("AUTO_REFUND_SEAT_COLLISION");

        // Assert
        changed.Should().BeTrue();
        reservation.Status.Should().Be(ReservationStatus.Cancelled);
        reservation.CancelledAt.Should().NotBeNull();
        reservation.CancellationReason.Should().Be("AUTO_REFUND_SEAT_COLLISION");
        reservation.Version.Should().Be(2);
    }

    [Fact]
    public void Cancel_ShouldReturnFalse_WhenAlreadyCancelledOrExpired()
    {
        // Arrange
        var reservation = ReservationState.Reconstitute(
            id: Guid.NewGuid(),
            customerId: Guid.NewGuid(),
            eventId: Guid.NewGuid(),
            status: ReservationStatus.Cancelled,
            totalAmount: 180.00m,
            currency: "BRL",
            version: 2);

        // Act
        var changed = reservation.Cancel("PAYMENT_DECLINED");

        // Assert
        changed.Should().BeFalse();
        reservation.Version.Should().Be(2);
    }

    [Fact]
    public void Cancel_ShouldThrowDomainException_WhenReasonIsEmpty()
    {
        // Arrange
        var reservation = ReservationState.Reconstitute(
            id: Guid.NewGuid(),
            customerId: Guid.NewGuid(),
            eventId: Guid.NewGuid(),
            status: ReservationStatus.ProcessingPayment,
            totalAmount: 180.00m,
            currency: "BRL",
            version: 1);

        // Act
        var act = () => reservation.Cancel("   ");

        // Assert
        act.Should().Throw<DomainException>()
            .WithMessage("*Cancellation reason cannot be empty*");
    }
}
