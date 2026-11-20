namespace TR.HW.ResTimeout.UnitTests.Messaging;

using Confluent.Kafka;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TR.HW.ResTimeout.Configuration;
using TR.HW.ResTimeout.Messaging;

public class KafkaConsumerTests
{
    private readonly Mock<IConsumer<string, string>> _consumerMock = new();
    private readonly Mock<IProducer<string, string>> _producerMock = new();
    private readonly Mock<ILogger<KafkaConsumer>> _loggerMock = new();
    private readonly KafkaConsumerOptions _options = new()
    {
        BootstrapServers = "localhost:9092",
        Topic = "reservation.v1.hold_created.delayed",
        GroupId = "cg-reservation-timeout",
        DeadLetterQueueTopic = "reservation.v1.hold_created.delayed.DLQ"
    };

    [Fact]
    public void Subscribe_ShouldCallConsumerSubscribe()
    {
        var sut = new KafkaConsumer(
            Options.Create(_options),
            _loggerMock.Object,
            _consumerMock.Object,
            _producerMock.Object);

        sut.Subscribe();

        _consumerMock.Verify(c => c.Subscribe(_options.Topic), Times.Once);
    }

    [Fact]
    public void Commit_ShouldCallConsumerCommit()
    {
        var sut = new KafkaConsumer(
            Options.Create(_options),
            _loggerMock.Object,
            _consumerMock.Object,
            _producerMock.Object);

        var consumeResult = new ConsumeResult<string, string>
        {
            Topic = _options.Topic,
            Partition = 0,
            Offset = 10,
            Message = new Message<string, string> { Key = "k1", Value = "v1" }
        };

        sut.Commit(consumeResult);

        _consumerMock.Verify(c => c.Commit(consumeResult), Times.Once);
    }

    [Fact]
    public async Task SendToDlqAsync_ShouldProduceToDlqTopic()
    {
        var sut = new KafkaConsumer(
            Options.Create(_options),
            _loggerMock.Object,
            _consumerMock.Object,
            _producerMock.Object);

        _producerMock.Setup(p => p.ProduceAsync(
                _options.DeadLetterQueueTopic,
                It.Is<Message<string, string>>(m => m.Key == "key-1" && m.Value == "bad-json"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeliveryResult<string, string> { Status = PersistenceStatus.Persisted });

        await sut.SendToDlqAsync("key-1", "bad-json", "Parse failure");

        _producerMock.Verify(p => p.ProduceAsync(
            _options.DeadLetterQueueTopic,
            It.Is<Message<string, string>>(m => m.Key == "key-1" && m.Value == "bad-json"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
