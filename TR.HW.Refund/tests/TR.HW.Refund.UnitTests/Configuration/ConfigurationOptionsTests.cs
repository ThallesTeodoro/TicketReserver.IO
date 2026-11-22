namespace TR.HW.Refund.UnitTests.Configuration;

using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TR.HW.Refund.Worker;

public sealed class ConfigurationOptionsTests
{
    [Fact]
    public void AddRefundWorkerServices_ShouldThrowArgumentNullException_WhenDatabaseConnectionStringIsMissing()
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
        var act = () => services.AddRefundWorkerServices(configuration);
        act.Should().Throw<ArgumentNullException>()
            .WithMessage("*Database:ConnectionString*");
    }

    [Fact]
    public void AddRefundWorkerServices_ShouldThrowArgumentNullException_WhenKafkaBootstrapServersIsMissing()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Database:ConnectionString", "Host=localhost;Database=payment_db" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();

        // Act & Assert
        var act = () => services.AddRefundWorkerServices(configuration);
        act.Should().Throw<ArgumentNullException>()
            .WithMessage("*Kafka:BootstrapServers*");
    }

    [Fact]
    public void AddRefundWorkerServices_ShouldRegisterServices_WhenConfigurationIsValid()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Database:ConnectionString", "Host=localhost;Database=payment_db;Username=user;Password=password" },
            { "Kafka:BootstrapServers", "localhost:9092" },
            { "KafkaConsumer:BootstrapServers", "localhost:9092" },
            { "PaymentGateway:Provider", "STRIPE" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddRefundWorkerServices(configuration);

        // Assert
        services.Should().Contain(d => d.ServiceType == typeof(TR.HW.Refund.Worker.Messaging.IKafkaConsumer));
        services.Should().Contain(d => d.ServiceType == typeof(TR.HW.Refund.Application.Interfaces.IRefundRepository));
        services.Should().Contain(d => d.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService));
    }
}
