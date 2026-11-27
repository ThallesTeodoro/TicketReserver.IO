using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TR.HW.Notification.Worker;
using TR.HW.Notification.Worker.Configuration;

namespace TR.HW.Notification.UnitTests.Configuration;

public class ConfigurationOptionsTests
{
    [Fact]
    public void AddWorkerServices_WithMissingBootstrapServers_ShouldThrowArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();
        var emptyConfig = new ConfigurationBuilder().Build();

        // Act
        var act = () => services.AddWorkerServices(emptyConfig);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithMessage("*BootstrapServers*");
    }

    [Fact]
    public void AddWorkerServices_WithValidBootstrapServers_ShouldRegisterDependencies()
    {
        // Arrange
        var services = new ServiceCollection();
        var inMemorySettings = new Dictionary<string, string?>
        {
            [$"{KafkaConsumerOptions.SectionName}:BootstrapServers"] = "localhost:9092"
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        // Act
        services.AddLogging();
        services.AddWorkerServices(config);
        var provider = services.BuildServiceProvider();

        // Assert
        provider.Should().NotBeNull();
    }
}
