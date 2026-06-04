using FluentAssertions;
using ReservationEntity = TR.API.Reservation.Domain.Entities.Reservation;
using TR.API.Reservation.Domain.Enums;
using TR.API.Reservation.Domain.Exceptions;

namespace TR.API.Reservation.UnitTests.Domain;

public class ReservationTests
{
    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateReservation()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var seatNumber = "A1";
        var price = 100.00m;

        // Act
        var reservation = new ReservationEntity(userId, eventId, seatNumber, price);

        // Assert
        reservation.Id.Should().NotBeEmpty();
        reservation.UserId.Should().Be(userId);
        reservation.EventId.Should().Be(eventId);
        reservation.SeatNumber.Should().Be(seatNumber);
        reservation.Price.Should().Be(price);
        reservation.Status.Should().Be(ReservationStatus.Pendente);
        reservation.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000", "5c9b4f2c-5b5c-4d5c-9c9c-9c9c9c9c9c9c", "A1", 100, "UserId")]
    [InlineData("5c9b4f2c-5b5c-4d5c-9c9c-9c9c9c9c9c9c", "00000000-0000-0000-0000-000000000000", "A1", 100, "EventId")]
    [InlineData("5c9b4f2c-5b5c-4d5c-9c9c-9c9c9c9c9c9c", "5c9b4f2c-5b5c-4d5c-9c9c-9c9c9c9c9c9c", "", 100, "SeatNumber")]
    [InlineData("5c9b4f2c-5b5c-4d5c-9c9c-9c9c9c9c9c9c", "5c9b4f2c-5b5c-4d5c-9c9c-9c9c9c9c9c9c", null, 100, "SeatNumber")]
    [InlineData("5c9b4f2c-5b5c-4d5c-9c9c-9c9c9c9c9c9c", "5c9b4f2c-5b5c-4d5c-9c9c-9c9c9c9c9c9c", "A1", 0, "Price")]
    [InlineData("5c9b4f2c-5b5c-4d5c-9c9c-9c9c9c9c9c9c", "5c9b4f2c-5b5c-4d5c-9c9c-9c9c9c9c9c9c", "A1", -1, "Price")]
    public void Constructor_WithInvalidParameters_ShouldThrowValidationException(string userIdStr, string eventIdStr, string? seatNumber, decimal price, string expectedParam)
    {
        // Arrange
        var userId = Guid.Parse(userIdStr);
        var eventId = Guid.Parse(eventIdStr);

        // Act
        Action act = () => new ReservationEntity(userId, eventId, seatNumber!, price);

        // Assert
        act.Should().Throw<ValidationException>().WithMessage($"*{expectedParam}*");
    }

    [Fact]
    public void Confirm_WhenStatusIsPendente_ShouldChangeStatusToConfirmada()
    {
        // Arrange
        var reservation = new ReservationEntity(Guid.NewGuid(), Guid.NewGuid(), "A1", 100);

        // Act
        reservation.Confirm();

        // Assert
        reservation.Status.Should().Be(ReservationStatus.Confirmada);
    }

    [Fact]
    public void Confirm_WhenStatusIsNotPendente_ShouldThrowBusinessException()
    {
        // Arrange
        var reservation = new ReservationEntity(Guid.NewGuid(), Guid.NewGuid(), "A1", 100);
        reservation.Confirm();

        // Act
        Action act = () => reservation.Confirm();

        // Assert
        act.Should().Throw<BusinessException>().WithMessage("*Cannot confirm reservation*");
    }

    [Fact]
    public void Cancel_WhenStatusIsNotCancelada_ShouldChangeStatusToCancelada()
    {
        // Arrange
        var reservation = new ReservationEntity(Guid.NewGuid(), Guid.NewGuid(), "A1", 100);

        // Act
        reservation.Cancel();

        // Assert
        reservation.Status.Should().Be(ReservationStatus.Cancelada);
    }

    [Fact]
    public void Cancel_WhenStatusIsCancelada_ShouldThrowBusinessException()
    {
        // Arrange
        var reservation = new ReservationEntity(Guid.NewGuid(), Guid.NewGuid(), "A1", 100);
        reservation.Cancel();

        // Act
        Action act = () => reservation.Cancel();

        // Assert
        act.Should().Throw<BusinessException>().WithMessage("*already cancelled*");
    }
}
