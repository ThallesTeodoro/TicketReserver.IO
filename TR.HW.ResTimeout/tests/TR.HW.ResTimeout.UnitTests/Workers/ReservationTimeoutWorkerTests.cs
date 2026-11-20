namespace TR.HW.ResTimeout.UnitTests.Workers;

using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TR.HW.ResTimeout.Caching;
using TR.HW.ResTimeout.Configuration;
using TR.HW.ResTimeout.Messaging;
using TR.HW.ResTimeout.Persistence;
using TR.HW.ResTimeout.Workers;

public class ReservationTimeoutWorkerTests
{
    private readonly Mock<IKafkaConsumer> _kafkaConsumerMock = new();
    private readonly Mock<IReservationTimeoutRepository> _repositoryMock = new();
    private readonly Mock<ISeatLockManager> _seatLockManagerMock = new();
    private readonly Mock<ILogger<ReservationTimeoutWorker>> _loggerMock = new();
    private readonly KafkaConsumerOptions _options = new()
    {
        Topic = "reservation.v1.hold_created.delayed",
        GroupId = "cg-reservation-timeout"
    };
    private readonly ReservationTimeoutWorker _sut;

    public ReservationTimeoutWorkerTests()
    {
        _sut = new ReservationTimeoutWorker(
            _kafkaConsumerMock.Object,
            _repositoryMock.Object,
            _seatLockManagerMock.Object,
            Options.Create(_options),
            _loggerMock.Object);
    }

    [Fact]
    public async Task ProcessMessageAsync_WhenAwaitingPayment_ShouldExpireReservationAndReleaseSeatLocks()
    {
        var reservationId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var seatId = Guid.NewGuid();
        var messageId = "msg-123";

        var payload = JsonSerializer.Serialize(new
        {
            id = messageId,
            aggregateId = reservationId,
            eventType = "reservation.hold_created"
        });

        var consumeResult = new ConsumeResult<string, string>
        {
            Topic = _options.Topic,
            Partition = 0,
            Offset = 100,
            Message = new Message<string, string>
            {
                Key = reservationId.ToString(),
                Value = payload,
                Headers = new Headers
                {
                    { "correlation_id", Encoding.UTF8.GetBytes(messageId) }
                }
            }
        };

        var reservationData = new ReservationTimeoutData(
            reservationId,
            "AWAITING_PAYMENT",
            eventId,
            Guid.NewGuid(),
            [new ReservationSeatItemData(seatId, "A-1")]);

        _repositoryMock.Setup(r => r.IsMessageProcessedAsync(messageId, _options.GroupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _repositoryMock.Setup(r => r.GetReservationAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservationData);

        _repositoryMock.Setup(r => r.ExpireReservationAsync(reservationId, messageId, _options.GroupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _sut.ProcessMessageAsync(consumeResult, CancellationToken.None);

        _repositoryMock.Verify(r => r.ExpireReservationAsync(reservationId, messageId, _options.GroupId, It.IsAny<CancellationToken>()), Times.Once);
        _seatLockManagerMock.Verify(s => s.ReleaseSeatLocksAsync(eventId, It.Is<IEnumerable<Guid>>(ids => ids.Contains(seatId)), reservationId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessMessageAsync_WhenProcessingPayment_ShouldNotExpireOrReleaseLocks()
    {
        var reservationId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var seatId = Guid.NewGuid();
        var messageId = "msg-456";

        var payload = JsonSerializer.Serialize(new
        {
            id = messageId,
            aggregateId = reservationId,
            eventType = "reservation.hold_created"
        });

        var consumeResult = new ConsumeResult<string, string>
        {
            Topic = _options.Topic,
            Partition = 0,
            Offset = 101,
            Message = new Message<string, string>
            {
                Key = reservationId.ToString(),
                Value = payload
            }
        };

        var reservationData = new ReservationTimeoutData(
            reservationId,
            "PROCESSING_PAYMENT",
            eventId,
            Guid.NewGuid(),
            [new ReservationSeatItemData(seatId, "A-1")]);

        _repositoryMock.Setup(r => r.IsMessageProcessedAsync(messageId, _options.GroupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _repositoryMock.Setup(r => r.GetReservationAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservationData);

        await _sut.ProcessMessageAsync(consumeResult, CancellationToken.None);

        _repositoryMock.Verify(r => r.ExpireReservationAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _seatLockManagerMock.Verify(s => s.ReleaseSeatLocksAsync(It.IsAny<Guid>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _repositoryMock.Verify(r => r.RecordProcessedMessageAsync(messageId, _options.GroupId, "reservation.v1.hold_created.delayed", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessMessageAsync_WhenConfirmed_ShouldNotExpireOrReleaseLocks()
    {
        var reservationId = Guid.NewGuid();
        var messageId = "msg-789";

        var payload = JsonSerializer.Serialize(new
        {
            id = messageId,
            aggregateId = reservationId
        });

        var consumeResult = new ConsumeResult<string, string>
        {
            Topic = _options.Topic,
            Partition = 0,
            Offset = 102,
            Message = new Message<string, string>
            {
                Key = reservationId.ToString(),
                Value = payload
            }
        };

        var reservationData = new ReservationTimeoutData(
            reservationId,
            "CONFIRMED",
            Guid.NewGuid(),
            Guid.NewGuid(),
            []);

        _repositoryMock.Setup(r => r.IsMessageProcessedAsync(messageId, _options.GroupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _repositoryMock.Setup(r => r.GetReservationAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reservationData);

        await _sut.ProcessMessageAsync(consumeResult, CancellationToken.None);

        _repositoryMock.Verify(r => r.ExpireReservationAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _seatLockManagerMock.Verify(s => s.ReleaseSeatLocksAsync(It.IsAny<Guid>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _repositoryMock.Verify(r => r.RecordProcessedMessageAsync(messageId, _options.GroupId, "reservation.v1.hold_created.delayed", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessMessageAsync_WhenAlreadyInInbox_ShouldSkipProcessing()
    {
        var reservationId = Guid.NewGuid();
        var messageId = "msg-inbox-dup";

        var payload = JsonSerializer.Serialize(new
        {
            id = messageId,
            aggregateId = reservationId
        });

        var consumeResult = new ConsumeResult<string, string>
        {
            Topic = _options.Topic,
            Partition = 0,
            Offset = 103,
            Message = new Message<string, string>
            {
                Key = reservationId.ToString(),
                Value = payload
            }
        };

        _repositoryMock.Setup(r => r.IsMessageProcessedAsync(messageId, _options.GroupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _sut.ProcessMessageAsync(consumeResult, CancellationToken.None);

        _repositoryMock.Verify(r => r.GetReservationAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _repositoryMock.Verify(r => r.ExpireReservationAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessMessageAsync_WhenReservationNotFound_ShouldRecordInboxAndSkip()
    {
        var reservationId = Guid.NewGuid();
        var messageId = "msg-notfound";

        var payload = JsonSerializer.Serialize(new
        {
            id = messageId,
            aggregateId = reservationId
        });

        var consumeResult = new ConsumeResult<string, string>
        {
            Topic = _options.Topic,
            Partition = 0,
            Offset = 104,
            Message = new Message<string, string>
            {
                Key = reservationId.ToString(),
                Value = payload
            }
        };

        _repositoryMock.Setup(r => r.IsMessageProcessedAsync(messageId, _options.GroupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _repositoryMock.Setup(r => r.GetReservationAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReservationTimeoutData?)null);

        await _sut.ProcessMessageAsync(consumeResult, CancellationToken.None);

        _repositoryMock.Verify(r => r.RecordProcessedMessageAsync(messageId, _options.GroupId, "reservation.v1.hold_created.delayed", It.IsAny<CancellationToken>()), Times.Once);
        _repositoryMock.Verify(r => r.ExpireReservationAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessMessageAsync_WhenInvalidJson_ShouldSendToDlq()
    {
        var consumeResult = new ConsumeResult<string, string>
        {
            Topic = _options.Topic,
            Partition = 0,
            Offset = 105,
            Message = new Message<string, string>
            {
                Key = "invalid-key",
                Value = "NOT_A_VALID_JSON"
            }
        };

        await _sut.ProcessMessageAsync(consumeResult, CancellationToken.None);

        _kafkaConsumerMock.Verify(k => k.SendToDlqAsync(
            "invalid-key",
            "NOT_A_VALID_JSON",
            It.Is<string>(reason => reason.Contains("Deserialization error")),
            It.IsAny<Headers>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _repositoryMock.Verify(r => r.IsMessageProcessedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessMessageAsync_WhenTraceparentPresent_ShouldProcessWithoutError()
    {
        var reservationId = Guid.NewGuid();
        var messageId = "msg-trace";
        var traceparent = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";

        var payload = JsonSerializer.Serialize(new
        {
            id = messageId,
            aggregateId = reservationId
        });

        var headers = new Headers
        {
            { "traceparent", Encoding.UTF8.GetBytes(traceparent) }
        };

        var consumeResult = new ConsumeResult<string, string>
        {
            Topic = _options.Topic,
            Partition = 0,
            Offset = 106,
            Message = new Message<string, string>
            {
                Key = reservationId.ToString(),
                Value = payload,
                Headers = headers
            }
        };

        _repositoryMock.Setup(r => r.IsMessageProcessedAsync(messageId, _options.GroupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _sut.ProcessMessageAsync(consumeResult, CancellationToken.None);

        _repositoryMock.Verify(r => r.IsMessageProcessedAsync(messageId, _options.GroupId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
