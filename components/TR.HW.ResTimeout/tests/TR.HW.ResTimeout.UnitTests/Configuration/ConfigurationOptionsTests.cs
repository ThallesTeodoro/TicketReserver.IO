namespace TR.HW.ResTimeout.UnitTests.Configuration;

using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TR.HW.ResTimeout.Configuration;

public class ConfigurationOptionsTests
{
    [Fact]
    public void KafkaConsumerOptions_ShouldHaveSensibleDefaults()
    {
        var options = new KafkaConsumerOptions();

        options.Topic.Should().Be("reservation.v1.hold_created.delayed");
        options.GroupId.Should().Be("cg-reservation-timeout");
        options.DeadLetterQueueTopic.Should().Be("reservation.v1.hold_created.delayed.DLQ");
        options.AutoOffsetReset.Should().Be("Earliest");
        options.EnableAutoCommit.Should().BeFalse();
        options.MaxPollIntervalMs.Should().Be(300000);
        options.SessionTimeoutMs.Should().Be(45000);
    }

    [Fact]
    public void DatabaseOptions_ShouldHaveSensibleDefaults()
    {
        var options = new DatabaseOptions();
        options.CommandTimeoutSeconds.Should().Be(30);
        options.ConnectionString.Should().BeEmpty();
    }

    [Fact]
    public void RedisOptions_ShouldHaveSensibleDefaults()
    {
        var options = new RedisOptions();
        options.DefaultDatabase.Should().Be(0);
        options.ConnectTimeout.Should().Be(5000);
        options.SyncTimeout.Should().Be(5000);
        options.ConnectionString.Should().BeEmpty();
    }

    [Fact]
    public void AddTimeoutInfrastructure_WhenDbConnectionStringMissing_ShouldThrowArgumentNullException()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kafka:BootstrapServers"] = "localhost:9092",
                ["ConnectionStrings:Redis"] = "localhost:6379"
            })
            .Build();

        var services = new ServiceCollection();

        var act = () => services.AddTimeoutInfrastructure(configuration);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("dbConnectionString");
    }

    [Fact]
    public void AddTimeoutInfrastructure_WhenKafkaBootstrapServersMissing_ShouldThrowArgumentNullException()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ReservationDb"] = "Host=localhost;Database=res",
                ["ConnectionStrings:Redis"] = "localhost:6379"
            })
            .Build();

        var services = new ServiceCollection();

        var act = () => services.AddTimeoutInfrastructure(configuration);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("kafkaBootstrapServers");
    }

    [Fact]
    public void AddTimeoutInfrastructure_WhenRedisConnectionStringMissing_ShouldThrowArgumentNullException()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ReservationDb"] = "Host=localhost;Database=res",
                ["Kafka:BootstrapServers"] = "localhost:9092"
            })
            .Build();

        var services = new ServiceCollection();

        var act = () => services.AddTimeoutInfrastructure(configuration);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("redisConnectionString");
    }
}
