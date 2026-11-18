namespace TR.API.Reservation.UnitTests.Application;

using Moq;
using TR.API.Reservation.Application.Reservations.CancelReservation;
using TR.API.Reservation.Domain.Contracts.Caching;
using TR.API.Reservation.Domain.Contracts.Persistence;
using TR.API.Reservation.Domain.Entities;
using TR.API.Reservation.Domain.Enums;
using TR.API.Reservation.Domain.Exceptions;

public class CancelReservationHandlerTests
{
    private readonly Mock<IReservationRepository> _reservationRepositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IHoldCacheService> _holdCacheServiceMock = new();
    private readonly CancelReservationCommandHandler _sut;

    public CancelReservationHandlerTests()
    {
        _sut = new CancelReservationCommandHandler(
            _reservationRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _holdCacheServiceMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WhenReservationNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        var reservationId = Guid.NewGuid();
        _reservationRepositoryMock
            .Setup(r => r.GetByIdAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Reservation?)null);

        var command = new CancelReservationCommand(reservationId, Guid.NewGuid());

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(() => _sut.HandleAsync(command));
    }

    [Fact]
    public async Task HandleAsync_WhenCustomerDoesNotMatch_ShouldThrowDomainException()
    {
        // Arrange
        var reservation = Reservation.Create(Guid.NewGuid(), Guid.NewGuid(), TimeSpan.FromMinutes(10));
        _reservationRepositoryMock
            .Setup(r => r.GetByIdAsync(reservation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);

        var differentCustomerId = Guid.NewGuid();
        var command = new CancelReservationCommand(reservation.Id, differentCustomerId);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<DomainException>(() => _sut.HandleAsync(command));
        Assert.Contains("pertence a outro cliente", ex.Message);
    }

    [Fact]
    public async Task HandleAsync_WhenStatusIsNotAwaitingPayment_ShouldThrowConflictException()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var reservation = Reservation.Create(customerId, Guid.NewGuid(), TimeSpan.FromMinutes(10));
        reservation.AddItem(Guid.NewGuid(), Guid.NewGuid(), "A-1", 1, 100m);
        reservation.StartPayment(DateTime.UtcNow); // moves to PROCESSING_PAYMENT

        _reservationRepositoryMock
            .Setup(r => r.GetByIdAsync(reservation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);

        var command = new CancelReservationCommand(reservation.Id, customerId);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(() => _sut.HandleAsync(command));
        Assert.Contains("Não é possível cancelar diretamente", ex.Message);
    }

    [Fact]
    public async Task HandleAsync_WhenValidAwaitingPayment_ShouldCancelAndReleaseHolds()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var reservation = Reservation.Create(customerId, eventId, TimeSpan.FromMinutes(10));
        var tierId = Guid.NewGuid();
        var seatId = Guid.NewGuid();
        reservation.AddItem(tierId, seatId, "B-5", 1, 120m);

        _reservationRepositoryMock
            .Setup(r => r.GetByIdAsync(reservation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservation);

        var command = new CancelReservationCommand(reservation.Id, customerId, "Desisti da compra");

        // Act
        var result = await _sut.HandleAsync(command);

        // Assert
        Assert.Equal(reservation.Id, result.ReservationId);
        Assert.Equal("CANCELLED", result.Status);
        Assert.Equal("Desisti da compra", result.CancellationReason);

        _holdCacheServiceMock.Verify(h => h.ReleaseReservationHoldsAsync(
            eventId,
            reservation.Id,
            It.Is<IEnumerable<HoldItemDescriptor>>(items => items.Any(i => i.SeatId == seatId)),
            It.IsAny<CancellationToken>()), Times.Once);

        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Validator_WhenFieldsAreEmpty_ShouldHaveValidationErrors()
    {
        var validator = new CancelReservationCommandValidator();
        var command = new CancelReservationCommand(Guid.Empty, Guid.Empty);

        var result = validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CancelReservationCommand.ReservationId));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CancelReservationCommand.CustomerId));
    }
}
