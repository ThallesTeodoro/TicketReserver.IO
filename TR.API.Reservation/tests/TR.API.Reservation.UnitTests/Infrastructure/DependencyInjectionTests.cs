namespace TR.API.Reservation.UnitTests.Infrastructure;

using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TR.API.Reservation.Infrastructure;

public class DependencyInjectionTests
{
    [Fact]
    public void AddInfrastructureServices_WhenReservationDbMissing_ShouldThrowArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "ConnectionStrings:Redis", "localhost:6379" }
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        // Act
        var act = () => services.AddInfrastructureServices(configuration);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithMessage("*ReservationDb*");
    }

    [Fact]
    public void AddInfrastructureServices_WhenRedisMissing_ShouldThrowArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "ConnectionStrings:ReservationDb", "Host=localhost;Database=test;Username=postgres;Password=postgres" }
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        // Act
        var act = () => services.AddInfrastructureServices(configuration);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithMessage("*Redis*");
    }
}
