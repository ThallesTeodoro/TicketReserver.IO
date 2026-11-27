namespace TR.HW.ReservationOutboxRelay.UnitTests.Infrastructure;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TR.HW.ReservationOutboxRelay;
using TR.HW.ReservationOutboxRelay.Configuration;

public class DependencyInjectionTests
{
    [Fact]
    public void AddRelayInfrastructure_WhenConnectionStringMissing_ShouldThrowArgumentNullException()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Kafka:BootstrapServers", "localhost:9094" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();

        var act = () => services.AddRelayInfrastructure(configuration);

        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void AddRelayInfrastructure_WhenKafkaBootstrapServersMissing_ShouldThrowArgumentNullException()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "ConnectionStrings:ReservationDb", "Host=localhost;Database=reservation_db" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();

        var act = () => services.AddRelayInfrastructure(configuration);

        Assert.Throws<ArgumentNullException>(act);
    }

    [Fact]
    public void AddRelayInfrastructure_WhenConfiguredCorrectly_ShouldBindOptions()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "ConnectionStrings:ReservationDb", "Host=localhost;Database=reservation_db" },
            { "Kafka:BootstrapServers", "localhost:9094" },
            { "Kafka:CheckoutTopic", "custom.checkout" },
            { "OutboxRelay:BatchSize", "100" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();
        services.AddRelayInfrastructure(configuration);

        var provider = services.BuildServiceProvider();

        var outboxOptions = provider.GetRequiredService<IOptions<OutboxRelayOptions>>().Value;
        var kafkaOptions = provider.GetRequiredService<IOptions<KafkaProducerOptions>>().Value;

        Assert.Equal("Host=localhost;Database=reservation_db", outboxOptions.ConnectionString);
        Assert.Equal(100, outboxOptions.BatchSize);
        Assert.Equal("localhost:9094", kafkaOptions.BootstrapServers);
        Assert.Equal("custom.checkout", kafkaOptions.CheckoutTopic);
    }
}
