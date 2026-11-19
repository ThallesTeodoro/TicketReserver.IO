namespace TR.HW.ReservationOutboxRelay.UnitTests.Workers;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TR.HW.ReservationOutboxRelay.Configuration;
using TR.HW.ReservationOutboxRelay.Messaging;
using TR.HW.ReservationOutboxRelay.Persistence;
using TR.HW.ReservationOutboxRelay.Workers;

public class ReservationOutboxRelayWorkerTests
{
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock = new();
    private readonly Mock<IServiceScope> _scopeMock = new();
    private readonly Mock<IServiceProvider> _serviceProviderMock = new();
    private readonly Mock<IOutboxRepository> _outboxRepositoryMock = new();
    private readonly Mock<IKafkaProducer> _kafkaProducerMock = new();
    private readonly Mock<ILogger<ReservationOutboxRelayWorker>> _loggerMock = new();

    private readonly OutboxRelayOptions _options = new()
    {
        BatchSize = 10,
        IntervalMilliseconds = 100,
        EmptyDelayMilliseconds = 200,
        MaxRetries = 5,
        RetryBaseDelaySeconds = 2
    };

    public ReservationOutboxRelayWorkerTests()
    {
        _scopeFactoryMock.Setup(s => s.CreateScope()).Returns(_scopeMock.Object);
        _scopeMock.Setup(s => s.ServiceProvider).Returns(_serviceProviderMock.Object);
        _serviceProviderMock.Setup(p => p.GetService(typeof(IOutboxRepository)))
            .Returns(_outboxRepositoryMock.Object);
    }

    private ReservationOutboxRelayWorker CreateSut()
    {
        var optionsMock = new Mock<IOptions<OutboxRelayOptions>>();
        optionsMock.Setup(o => o.Value).Returns(_options);

        return new ReservationOutboxRelayWorker(
            _scopeFactoryMock.Object,
            _kafkaProducerMock.Object,
            optionsMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task ProcessBatchAsync_WhenNoMessages_ShouldReturnZero()
    {
        // Arrange
        _outboxRepositoryMock
            .Setup(r => r.GetPendingBatchAsync(_options.BatchSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OutboxMessageRecord>());

        var sut = CreateSut();

        // Act
        var result = await sut.ProcessBatchAsync();

        // Assert
        Assert.Equal(0, result);
        _kafkaProducerMock.Verify(k => k.ProduceAsync(It.IsAny<OutboxMessageRecord>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessBatchAsync_WhenMessageSucceeds_ShouldProduceAndMarkPublished()
    {
        // Arrange
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

        _outboxRepositoryMock
            .Setup(r => r.GetPendingBatchAsync(_options.BatchSize, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OutboxMessageRecord> { message });

        var sut = CreateSut();

        // Act
        var result = await sut.ProcessBatchAsync();

        // Assert
        Assert.Equal(1, result);
        _kafkaProducerMock.Verify(k => k.ProduceAsync(message, It.IsAny<CancellationToken>()), Times.Once);
        _outboxRepositoryMock.Verify(r => r.MarkAsPublishedAsync(message.Id, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessSingleMessageAsync_WhenProduceFails_AndRetriesRemain_ShouldIncrementRetry()
    {
        // Arrange
        var message = new OutboxMessageRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "reservation.checkout_req",
            "Reservation",
            Guid.NewGuid(),
            "{}",
            0,
            DateTime.UtcNow,
            0,
            1, // RetryCount = 1
            null,
            null,
            DateTime.UtcNow);

        _kafkaProducerMock
            .Setup(k => k.ProduceAsync(message, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Falha de rede com o Kafka"));

        var sut = CreateSut();

        // Act
        await sut.ProcessSingleMessageAsync(_outboxRepositoryMock.Object, message, CancellationToken.None);

        // Assert
        _outboxRepositoryMock.Verify(r => r.IncrementRetryAsync(
            message.Id,
            "Falha de rede com o Kafka",
            4, // (1 + 1) * 2 = 4
            It.IsAny<CancellationToken>()), Times.Once);

        _outboxRepositoryMock.Verify(r => r.MarkAsFailedAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessSingleMessageAsync_WhenMaxRetriesExceeded_ShouldSendToDlqAndMarkAsFailed()
    {
        // Arrange
        var message = new OutboxMessageRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "reservation.checkout_req",
            "Reservation",
            Guid.NewGuid(),
            "{}",
            0,
            DateTime.UtcNow,
            0,
            4, // RetryCount = 4 (4 + 1 >= 5 = MaxRetries)
            null,
            null,
            DateTime.UtcNow);

        _kafkaProducerMock
            .Setup(k => k.ProduceAsync(message, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Falha permanente de conexão"));

        var sut = CreateSut();

        // Act
        await sut.ProcessSingleMessageAsync(_outboxRepositoryMock.Object, message, CancellationToken.None);

        // Assert
        _kafkaProducerMock.Verify(k => k.ProduceToDeadLetterQueueAsync(
            message,
            "Falha permanente de conexão",
            It.IsAny<CancellationToken>()), Times.Once);

        _outboxRepositoryMock.Verify(r => r.MarkAsFailedAsync(
            message.Id,
            "Falha permanente de conexão",
            It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
