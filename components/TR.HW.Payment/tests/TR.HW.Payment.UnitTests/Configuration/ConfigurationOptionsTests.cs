namespace TR.HW.Payment.UnitTests.Configuration;

using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TR.HW.Payment.Worker;

public sealed class ConfigurationOptionsTests
{
    [Fact]
    public void AddPaymentWorkerServices_ShouldThrowArgumentNullException_WhenDatabaseConnectionStringIsMissing()
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
        var act = () => services.AddPaymentWorkerServices(configuration);
        act.Should().Throw<ArgumentNullException>()
            .WithMessage("*Database:ConnectionString*");
    }

    [Fact]
    public void AddPaymentWorkerServices_ShouldThrowArgumentNullException_WhenKafkaBootstrapServersIsMissing()
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
        var act = () => services.AddPaymentWorkerServices(configuration);
        act.Should().Throw<ArgumentNullException>()
            .WithMessage("*Kafka:BootstrapServers*");
    }

    [Fact]
    public void AddPaymentWorkerServices_ShouldRegisterServices_WhenConfigurationIsValid()
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
        services.AddPaymentWorkerServices(configuration);

        // Assert
        services.Should().Contain(d => d.ServiceType == typeof(TR.HW.Payment.Worker.Messaging.IKafkaConsumer));
        services.Should().Contain(d => d.ServiceType == typeof(TR.HW.Payment.Application.Interfaces.IPaymentRepository));
        services.Should().Contain(d => d.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService));
    }
}
