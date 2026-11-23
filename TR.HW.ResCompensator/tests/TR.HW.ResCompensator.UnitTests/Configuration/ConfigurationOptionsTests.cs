namespace TR.HW.ResCompensator.UnitTests.Configuration;

using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TR.HW.ResCompensator.Worker;

public sealed class ConfigurationOptionsTests
{
    [Fact]
    public void AddReservationCompensatorWorkerServices_ShouldThrowArgumentNullException_WhenDatabaseConnectionStringIsMissing()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Redis:ConnectionString", "localhost:6379" },
            { "KafkaConsumer:BootstrapServers", "localhost:9092" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();

        // Act & Assert
        var act = () => services.AddReservationCompensatorWorkerServices(configuration);
        act.Should().Throw<ArgumentNullException>()
            .WithMessage("*Database:ConnectionString*");
    }

    [Fact]
    public void AddReservationCompensatorWorkerServices_ShouldThrowArgumentNullException_WhenRedisConnectionStringIsMissing()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Database:ConnectionString", "Host=localhost;Database=reservation_db" },
            { "KafkaConsumer:BootstrapServers", "localhost:9092" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();

        // Act & Assert
        var act = () => services.AddReservationCompensatorWorkerServices(configuration);
        act.Should().Throw<ArgumentNullException>()
            .WithMessage("*Redis:ConnectionString*");
    }

    [Fact]
    public void AddReservationCompensatorWorkerServices_ShouldThrowArgumentNullException_WhenKafkaBootstrapServersIsMissing()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Database:ConnectionString", "Host=localhost;Database=reservation_db" },
            { "Redis:ConnectionString", "localhost:6379" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();

        // Act & Assert
        var act = () => services.AddReservationCompensatorWorkerServices(configuration);
        act.Should().Throw<ArgumentNullException>()
            .WithMessage("*KafkaConsumer:BootstrapServers*");
    }

    [Fact]
    public void AddReservationCompensatorWorkerServices_ShouldRegisterServices_WhenConfigurationIsValid()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Database:ConnectionString", "Host=localhost;Database=reservation_db;Username=user;Password=password" },
            { "Redis:ConnectionString", "localhost:6379,abortConnect=false" },
            { "KafkaConsumer:BootstrapServers", "localhost:9092" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddReservationCompensatorWorkerServices(configuration);

        // Assert
        services.Should().Contain(d => d.ServiceType == typeof(TR.HW.ResCompensator.Worker.Messaging.IKafkaConsumer));
        services.Should().Contain(d => d.ServiceType == typeof(TR.HW.ResCompensator.Application.Interfaces.IReservationCompensationRepository));
        services.Should().Contain(d => d.ServiceType == typeof(TR.HW.ResCompensator.Application.Interfaces.IRedisSeatLockReleaser));
        services.Should().Contain(d => d.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService));
    }
}
