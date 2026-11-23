using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TR.HW.Repl.Worker;
using TR.HW.Repl.Worker.Configuration;
using TR.HW.Repl.Worker.Messaging;
using Xunit;

namespace TR.HW.Repl.UnitTests.Configuration;

public class ConfigurationOptionsTests
{
    [Fact]
    public void AddWorkerServices_WhenBootstrapServersMissing_ShouldThrowArgumentNullException()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KafkaConsumer:GroupId"] = "cg-test"
            })
            .Build();

        // Act
        Action act = () => services.AddWorkerServices(configuration);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("BootstrapServers");
    }

    [Fact]
    public void AddWorkerServices_WhenValidConfiguration_ShouldRegisterServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["KafkaConsumer:BootstrapServers"] = "localhost:9092",
                ["KafkaConsumer:GroupId"] = "cg-search-replication-worker"
            })
            .Build();

        // Act
        services.AddLogging();
        services.AddWorkerServices(configuration);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var consumer = serviceProvider.GetService<IKafkaConsumer>();
        consumer.Should().NotBeNull();
    }
}
