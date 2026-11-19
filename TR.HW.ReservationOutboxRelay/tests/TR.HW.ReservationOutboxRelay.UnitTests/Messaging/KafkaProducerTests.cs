namespace TR.HW.ReservationOutboxRelay.UnitTests.Messaging;

using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TR.HW.ReservationOutboxRelay.Configuration;
using TR.HW.ReservationOutboxRelay.Messaging;
using TR.HW.ReservationOutboxRelay.Persistence;

public class KafkaProducerTests
{
    private readonly Mock<IProducer<string, string>> _mockProducer = new();
    private readonly Mock<ILogger<KafkaProducer>> _mockLogger = new();
    private readonly KafkaProducerOptions _options = new()
    {
        BootstrapServers = "localhost:9092",
        CheckoutTopic = "reservation.v1.checkout_req",
        HoldCreatedDelayedTopic = "reservation.v1.hold_created.delayed",
        DeadLetterQueueTopic = "reservation.v1.checkout_req.DLQ",
        ClientId = "test-client"
    };

    private KafkaProducer CreateSut()
    {
        var optionsMock = new Mock<IOptions<KafkaProducerOptions>>();
        optionsMock.Setup(o => o.Value).Returns(_options);
        return new KafkaProducer(optionsMock.Object, _mockLogger.Object, _mockProducer.Object);
    }

    [Theory]
    [InlineData("reservation.checkout_req", "reservation.v1.checkout_req")]
    [InlineData("reservation.hold_created", "reservation.v1.hold_created.delayed")]
    [InlineData("unknown_event", "reservation.v1.checkout_req.DLQ")]
    public void ResolveTopic_ShouldReturnExpectedTopic(string eventType, string expectedTopic)
    {
        using var sut = CreateSut();
        var topic = sut.ResolveTopic(eventType);
        Assert.Equal(expectedTopic, topic);
    }

    [Fact]
    public async Task ProduceAsync_WhenDeliveryPersisted_ShouldSucceed()
    {
        // Arrange
        using var sut = CreateSut();
        var message = new OutboxMessageRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "reservation.checkout_req",
            "Reservation",
            Guid.NewGuid(),
            "{\"amount\":100}",
            0,
            DateTime.UtcNow,
            0,
            0,
            null,
            null,
            DateTime.UtcNow);

        _mockProducer
            .Setup(p => p.ProduceAsync(
                _options.CheckoutTopic,
                It.Is<Message<string, string>>(m =>
                    m.Key == message.AggregateId.ToString() &&
                    m.Headers.Any(h => h.Key == "correlation_id") &&
                    m.Headers.Any(h => h.Key == "event_type") &&
                    m.Headers.Any(h => h.Key == "traceparent")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeliveryResult<string, string> { Status = PersistenceStatus.Persisted });

        // Act & Assert
        await sut.ProduceAsync(message);

        _mockProducer.Verify(p => p.ProduceAsync(
            _options.CheckoutTopic,
            It.IsAny<Message<string, string>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProduceToDeadLetterQueueAsync_ShouldProduceToDlqWithReasonHeader()
    {
        // Arrange
        using var sut = CreateSut();
        var message = new OutboxMessageRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "invalid_event",
            "Reservation",
            Guid.NewGuid(),
            "{}",
            0,
            DateTime.UtcNow,
            2,
            5,
            "Max retries exceeded",
            null,
            DateTime.UtcNow);

        _mockProducer
            .Setup(p => p.ProduceAsync(
                _options.DeadLetterQueueTopic,
                It.Is<Message<string, string>>(m =>
                    m.Key == message.AggregateId.ToString() &&
                    m.Headers.Any(h => h.Key == "dlq_reason" && Encoding.UTF8.GetString(h.GetValueBytes()) == "Falha fatal")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeliveryResult<string, string> { Status = PersistenceStatus.Persisted });

        // Act
        await sut.ProduceToDeadLetterQueueAsync(message, "Falha fatal");

        // Assert
        _mockProducer.Verify(p => p.ProduceAsync(
            _options.DeadLetterQueueTopic,
            It.IsAny<Message<string, string>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
