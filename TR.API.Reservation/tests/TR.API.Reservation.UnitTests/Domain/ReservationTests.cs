namespace TR.API.Reservation.UnitTests.Domain;

using FluentAssertions;
using TR.API.Reservation.Domain.Entities;
using TR.API.Reservation.Domain.Enums;
using TR.API.Reservation.Domain.Exceptions;

public class ReservationTests
{
    [Fact]
    public void Create_WithValidParameters_ShouldInstantiateReservationInAwaitingPaymentStatus()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var holdDuration = TimeSpan.FromMinutes(10);

        // Act
        var reservation = Reservation.Create(customerId, eventId, holdDuration, "BRL");

        // Assert
        reservation.Should().NotBeNull();
        reservation.Id.Should().NotBeEmpty();
        reservation.CustomerId.Should().Be(customerId);
        reservation.EventId.Should().Be(eventId);
        reservation.Status.Should().Be(ReservationStatus.AWAITING_PAYMENT);
        reservation.Currency.Should().Be("BRL");
        reservation.TotalAmount.Should().Be(0m);
        reservation.Version.Should().Be(1);
        reservation.HoldExpiresAt.Should().BeAfter(DateTime.UtcNow);
        reservation.Items.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithEmptyCustomerId_ShouldThrowDomainException()
    {
        // Act
        var act = () => Reservation.Create(Guid.Empty, Guid.NewGuid(), TimeSpan.FromMinutes(10));

        // Assert
        act.Should().Throw<DomainException>().WithMessage("*CustomerId não pode ser vazio*");
    }

    [Fact]
    public void Create_WithInvalidHoldDuration_ShouldThrowDomainException()
    {
        // Act
        var act = () => Reservation.Create(Guid.NewGuid(), Guid.NewGuid(), TimeSpan.Zero);

        // Assert
        act.Should().Throw<DomainException>().WithMessage("*duração do hold temporário deve ser maior que zero*");
    }

    [Fact]
    public void AddItem_WhenReservationIsAwaitingPayment_ShouldAddItemAndRecalculateTotal()
    {
        // Arrange
        var reservation = Reservation.Create(Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromMinutes(10));
        var tierId = Guid.NewGuid();
        var seatId = Guid.NewGuid();

        // Act
        reservation.AddItem(tierId, seatId, "A-12", 2, 150.50m);

        // Assert
        reservation.Items.Should().HaveCount(1);
        reservation.TotalAmount.Should().Be(301.00m);
        var item = reservation.Items.First();
        item.SeatLabel.Should().Be("A-12");
        item.Quantity.Should().Be(2);
        item.UnitPrice.Should().Be(150.50m);
        item.Subtotal.Should().Be(301.00m);
    }

    [Fact]
    public void StartPayment_WithValidItemsAndWithinHold_ShouldTransitionToProcessingPayment()
    {
        // Arrange
        var reservation = Reservation.Create(Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromMinutes(10));
        reservation.AddItem(Guid.NewGuid(), null, null, 1, 100m);
        var now = DateTime.UtcNow;

        // Act
        reservation.StartPayment(now);

        // Assert
        reservation.Status.Should().Be(ReservationStatus.PROCESSING_PAYMENT);
        reservation.PaymentAttemptedAt.Should().Be(now);
        reservation.Version.Should().Be(2);
    }

    [Fact]
    public void StartPayment_WhenHoldExpired_ShouldTransitionToExpiredAndThrowConflictException()
    {
        // Arrange
        var reservation = Reservation.Create(Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromMinutes(10));
        reservation.AddItem(Guid.NewGuid(), null, null, 1, 100m);
        var expiredTime = reservation.HoldExpiresAt.AddMinutes(1);

        // Act
        var act = () => reservation.StartPayment(expiredTime);

        // Assert
        act.Should().Throw<ConflictException>().WithMessage("*expirou*");
        reservation.Status.Should().Be(ReservationStatus.EXPIRED);
    }

    [Fact]
    public void ConfirmPayment_WhenProcessingPayment_ShouldTransitionToConfirmed()
    {
        // Arrange
        var reservation = Reservation.Create(Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromMinutes(10));
        reservation.AddItem(Guid.NewGuid(), null, null, 1, 100m);
        reservation.StartPayment(DateTime.UtcNow);
        var confirmedAt = DateTime.UtcNow;

        // Act
        reservation.ConfirmPayment(confirmedAt);

        // Assert
        reservation.Status.Should().Be(ReservationStatus.CONFIRMED);
        reservation.ConfirmedAt.Should().Be(confirmedAt);
        reservation.Version.Should().Be(3);
    }

    [Fact]
    public void Expire_WhenAlreadyConfirmed_ShouldThrowConflictException()
    {
        // Arrange
        var reservation = Reservation.Create(Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromMinutes(10));
        reservation.AddItem(Guid.NewGuid(), null, null, 1, 100m);
        reservation.StartPayment(DateTime.UtcNow);
        reservation.ConfirmPayment(DateTime.UtcNow);

        // Act
        var act = () => reservation.Expire(DateTime.UtcNow);

        // Assert
        act.Should().Throw<ConflictException>().WithMessage("*não pode ser expirada*");
    }

    [Fact]
    public void Cancel_WhenAlreadyConfirmed_ShouldThrowConflictException()
    {
        // Arrange
        var reservation = Reservation.Create(Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromMinutes(10));
        reservation.AddItem(Guid.NewGuid(), null, null, 1, 100m);
        reservation.StartPayment(DateTime.UtcNow);
        reservation.ConfirmPayment(DateTime.UtcNow);

        // Act
        var act = () => reservation.Cancel("User requested", DateTime.UtcNow);

        // Assert
        act.Should().Throw<ConflictException>().WithMessage("*estorno*");
    }
}
