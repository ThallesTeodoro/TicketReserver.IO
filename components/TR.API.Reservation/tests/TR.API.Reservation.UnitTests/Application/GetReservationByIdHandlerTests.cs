namespace TR.API.Reservation.UnitTests.Application;

using Moq;
using TR.API.Reservation.Application.Reservations.GetReservationById;
using TR.API.Reservation.Domain.Contracts.Persistence;
using TR.API.Reservation.Domain.Exceptions;

public class GetReservationByIdHandlerTests
{
    private readonly Mock<IReservationReadRepository> _readRepositoryMock = new();
    private readonly GetReservationByIdQueryHandler _sut;

    public GetReservationByIdHandlerTests()
    {
        _sut = new GetReservationByIdQueryHandler(_readRepositoryMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WhenReservationNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        var reservationId = Guid.NewGuid();
        _readRepositoryMock
            .Setup(r => r.GetByIdAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReservationReadModel?)null);

        var query = new GetReservationByIdQuery(reservationId);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() => _sut.HandleAsync(query));
        Assert.Contains(reservationId.ToString(), ex.Message);
    }

    [Fact]
    public async Task HandleAsync_WhenReservationExists_ShouldReturnDetailsResponse()
    {
        // Arrange
        var reservationId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var tierId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var items = new List<ReservationItemReadModel>
        {
            new(Guid.NewGuid(), tierId, Guid.NewGuid(), "A-10", 1, 150.00m, 150.00m, now)
        };

        var readModel = new ReservationReadModel(
            reservationId,
            customerId,
            eventId,
            "AWAITING_PAYMENT",
            150.00m,
            "BRL",
            now.AddMinutes(8),
            null,
            null,
            null,
            null,
            1,
            now,
            now,
            480,
            false,
            items);

        _readRepositoryMock
            .Setup(r => r.GetByIdAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(readModel);

        var query = new GetReservationByIdQuery(reservationId);

        // Act
        var result = await _sut.HandleAsync(query);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(reservationId, result.Id);
        Assert.Equal(customerId, result.CustomerId);
        Assert.Equal(eventId, result.EventId);
        Assert.Equal("AWAITING_PAYMENT", result.Status);
        Assert.Equal(150.00m, result.TotalAmount);
        Assert.Equal(480, result.RemainingHoldSeconds);
        Assert.False(result.IsExpired);
        Assert.Single(result.Items);
        Assert.Equal("A-10", result.Items.First().SeatLabel);
    }
}
