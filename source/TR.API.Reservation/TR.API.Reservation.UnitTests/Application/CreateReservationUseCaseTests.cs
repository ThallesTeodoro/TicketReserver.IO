using FluentAssertions;
using Moq;
using TR.API.Reservation.Application.UseCases.CreateReservation;
using TR.API.Reservation.Domain.Repositories;
using ReservationEntity = TR.API.Reservation.Domain.Entities.Reservation;

namespace TR.API.Reservation.UnitTests.Application;

public class CreateReservationUseCaseTests
{
    private readonly Mock<IReservationRepository> _repositoryMock;
    private readonly CreateReservationUseCase _useCase;

    public CreateReservationUseCaseTests()
    {
        _repositoryMock = new Mock<IReservationRepository>();
        _useCase = new CreateReservationUseCase(_repositoryMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidCommand_ShouldCreateReservationAndReturnOutput()
    {
        // Arrange
        var command = new CreateReservationCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "A1",
            100.00m);

        // Act
        var result = await _useCase.ExecuteAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.UserId.Should().Be(command.UserId);
        result.EventId.Should().Be(command.EventId);
        result.SeatNumber.Should().Be(command.SeatNumber);
        result.Price.Should().Be(command.Price);
        result.Status.Should().Be("Pendente");

        _repositoryMock.Verify(r => r.AddAsync(It.Is<ReservationEntity>(res =>
            res.UserId == command.UserId &&
            res.EventId == command.EventId &&
            res.SeatNumber == command.SeatNumber &&
            res.Price == command.Price
        )), Times.Once);
    }
}
