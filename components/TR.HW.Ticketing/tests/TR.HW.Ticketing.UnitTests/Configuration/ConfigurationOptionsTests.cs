namespace TR.HW.Ticketing.UnitTests.Configuration;

using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TR.HW.Ticketing.Worker;

public sealed class ConfigurationOptionsTests
{
    [Fact]
    public void AddTicketingWorkerServices_ShouldThrowArgumentNullException_WhenDatabaseConnectionStringIsMissing()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Kafka:BootstrapServers", "localhost:9092" },
            { "KafkaConsumer:BootstrapServers", "localhost:9092" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();

        // Act & Assert
        var act = () => services.AddTicketingWorkerServices(configuration);
        act.Should().Throw<ArgumentNullException>()
            .WithMessage("*Database:ConnectionString*");
    }

    [Fact]
    public void AddTicketingWorkerServices_ShouldThrowArgumentNullException_WhenKafkaBootstrapServersIsMissing()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Database:ConnectionString", "Host=localhost;Database=ticketing_db" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();

        // Act & Assert
        var act = () => services.AddTicketingWorkerServices(configuration);
        act.Should().Throw<ArgumentNullException>()
            .WithMessage("*Kafka:BootstrapServers*");
    }

    [Fact]
    public void AddTicketingWorkerServices_ShouldRegisterServices_WhenConfigurationIsValid()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Database:ConnectionString", "Host=localhost;Database=ticketing_db;Username=user;Password=password" },
            { "Kafka:BootstrapServers", "localhost:9092" },
            { "KafkaConsumer:BootstrapServers", "localhost:9092" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddTicketingWorkerServices(configuration);

        // Assert
        services.Should().Contain(d => d.ServiceType == typeof(TR.HW.Ticketing.Worker.Messaging.IKafkaConsumer));
        services.Should().Contain(d => d.ServiceType == typeof(TR.HW.Ticketing.Application.Interfaces.ITicketingRepository));
        services.Should().Contain(d => d.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService));
    }
}
