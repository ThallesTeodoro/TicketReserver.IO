namespace TR.API.Reservation.UnitTests.Application;

using FluentAssertions;
using FluentValidation.TestHelper;
using Moq;
using TR.API.Reservation.Application.Reservations.HoldReservation;
using TR.API.Reservation.Domain.Contracts.Caching;
using TR.API.Reservation.Domain.Contracts.Persistence;
using TR.API.Reservation.Domain.Entities;
using TR.API.Reservation.Domain.Enums;
using TR.API.Reservation.Domain.Exceptions;

public class HoldReservationHandlerTests
{
    private readonly Mock<IHoldCacheService> _cacheMock = new();
    private readonly Mock<IReservationRepository> _repoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly HoldReservationCommandHandler _sut;
    private readonly HoldReservationCommandValidator _validator = new();

    public HoldReservationHandlerTests()
    {
        _sut = new HoldReservationCommandHandler(_cacheMock.Object, _repoMock.Object, _uowMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WhenItemsAvailable_ShouldAcquireLocksAndPersistReservationWithOutbox()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var seatId = Guid.NewGuid();
        var tierId = Guid.NewGuid();

        _cacheMock.Setup(c => c.AcquireSeatHoldAsync(eventId, seatId, It.IsAny<Guid>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _uowMock.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var items = new[] { new HoldReservationItemDto(tierId, seatId, "A-1", 1, 150m) };
        var command = new HoldReservationCommand(customerId, eventId, items);

        // Act
        var result = await _sut.HandleAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(ReservationStatus.AWAITING_PAYMENT.ToString());
        result.TotalAmount.Should().Be(150m);
        result.Items.Should().HaveCount(1);

        _repoMock.Verify(r => r.AddAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.AddOutboxMessageAsync(It.Is<OutboxMessage>(m => m.EventType == "reservation.hold_created" && m.DelaySeconds == 600), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenSeatUnavailable_ShouldRollbackAndThrowConflictException()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var seat1 = Guid.NewGuid();
        var seat2 = Guid.NewGuid();
        var tierId = Guid.NewGuid();

        _cacheMock.Setup(c => c.AcquireSeatHoldAsync(eventId, seat1, It.IsAny<Guid>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _cacheMock.Setup(c => c.AcquireSeatHoldAsync(eventId, seat2, It.IsAny<Guid>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var items = new[]
        {
            new HoldReservationItemDto(tierId, seat1, "A-1", 1, 100m),
            new HoldReservationItemDto(tierId, seat2, "A-2", 1, 100m)
        };
        var command = new HoldReservationCommand(customerId, eventId, items);

        // Act
        var act = async () => await _sut.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ConflictException>().WithMessage("*já está reservado*");
        _cacheMock.Verify(c => c.ReleaseReservationHoldsAsync(eventId, It.IsAny<Guid>(), It.IsAny<IEnumerable<HoldItemDescriptor>>(), It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.AddAsync(It.IsAny<Reservation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenDatabaseFails_ShouldReleaseLocksAndRethrow()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var seatId = Guid.NewGuid();

        _cacheMock.Setup(c => c.AcquireSeatHoldAsync(eventId, seatId, It.IsAny<Guid>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _uowMock.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB connection dropped"));

        var items = new[] { new HoldReservationItemDto(Guid.NewGuid(), seatId, "A-1", 1, 100m) };
        var command = new HoldReservationCommand(customerId, eventId, items);

        // Act
        var act = async () => await _sut.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("DB connection dropped");
        _cacheMock.Verify(c => c.ReleaseReservationHoldsAsync(eventId, It.IsAny<Guid>(), It.IsAny<IEnumerable<HoldItemDescriptor>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Validator_WhenItemsEmpty_ShouldHaveValidationError()
    {
        // Arrange
        var command = new HoldReservationCommand(Guid.NewGuid(), Guid.NewGuid(), Array.Empty<HoldReservationItemDto>());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Items);
    }
}
