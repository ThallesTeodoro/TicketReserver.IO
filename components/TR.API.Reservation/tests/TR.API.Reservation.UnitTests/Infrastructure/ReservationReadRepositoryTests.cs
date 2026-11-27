namespace TR.API.Reservation.UnitTests.Infrastructure;

using System.Data;
using FluentAssertions;
using Moq;
using TR.API.Reservation.Infrastructure.Persistence;
using TR.API.Reservation.Infrastructure.Persistence.Repositories;

public class ReservationReadRepositoryTests
{
    [Fact]
    public async Task GetByIdAsync_WhenConnectionFails_ShouldPropagateException()
    {
        // Arrange
        var factoryMock = new Mock<IDbConnectionFactory>();
        factoryMock.Setup(f => f.CreateConnectionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Connection failed"));

        var repository = new ReservationReadRepository(factoryMock.Object);

        // Act
        var act = async () => await repository.GetByIdAsync(Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Connection failed");
    }
}
