namespace TR.HW.ResCompensator.UnitTests.Workers;

using System.Text.Json;
using Confluent.Kafka;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using TR.HW.ResCompensator.Application.Common.Messaging;
using TR.HW.ResCompensator.Application.Features.ReconcileReservation;
using TR.HW.ResCompensator.Application.Interfaces;
using TR.HW.ResCompensator.Domain.Enums;
using TR.HW.ResCompensator.Worker.Configuration;
using TR.HW.ResCompensator.Worker.Messaging;
using TR.HW.ResCompensator.Worker.Workers;

public sealed class ReservationCompensationWorkerTests
{
    private readonly Mock<IKafkaConsumer> _mockKafkaConsumer;
    private readonly Mock<IReservationCompensationRepository> _mockRepository;
    private readonly Mock<ICommandHandler<ReconcileReservationOutcomeCommand, ReconcileReservationOutcomeResult>> _mockHandler;
    private readonly KafkaConsumerOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;

    public ReservationCompensationWorkerTests()
    {
        _mockKafkaConsumer = new Mock<IKafkaConsumer>();
        _mockRepository = new Mock<IReservationCompensationRepository>();
        _mockHandler = new Mock<ICommandHandler<ReconcileReservationOutcomeCommand, ReconcileReservationOutcomeResult>>();

        _options = new KafkaConsumerOptions
        {
            Topics = ["payment.v1.refunded", "payment.v1.declined", "ticketing.v1.issued"],
            GroupId = "cg-reservation-compensator",
            DeadLetterQueueTopic = "payment.v1.refunded.DLQ"
        };

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddScoped(_ => _mockRepository.Object);
        serviceCollection.AddScoped(_ => _mockHandler.Object);
        var provider = serviceCollection.BuildServiceProvider();
        _scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
    }

    [Fact]
    public async Task ProcessMessageAsync_ShouldDispatchConfirmedOutcome_WhenTicketingIssuedMessageReceived()
    {
        // Arrange
        var reservationId = Guid.NewGuid();
        var rawJson = JsonSerializer.Serialize(new
        {
            eventId = Guid.NewGuid().ToString(),
            eventType = "ticketing.v1.issued",
            reservationId
        });

        var consumeResult = new ConsumeResult<string, string>
        {
            Topic = "ticketing.v1.issued",
            Partition = new Partition(0),
            Offset = new Offset(10),
            Message = new Message<string, string>
            {
                Key = reservationId.ToString(),
                Value = rawJson
            }
        };

        _mockRepository
            .Setup(r => r.IsMessageProcessedAsync(It.IsAny<string>(), _options.GroupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockHandler
            .Setup(h => h.HandleAsync(It.IsAny<ReconcileReservationOutcomeCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReconcileReservationOutcomeResult(true, reservationId, "CONFIRMED", 0));

        var worker = new ReservationCompensationWorker(
            _mockKafkaConsumer.Object,
            _scopeFactory,
            Options.Create(_options),
            NullLogger<ReservationCompensationWorker>.Instance);

        // Act
        await worker.ProcessMessageAsync(consumeResult, CancellationToken.None);

        // Assert
        _mockHandler.Verify(h => h.HandleAsync(
            It.Is<ReconcileReservationOutcomeCommand>(cmd =>
                cmd.ReservationId == reservationId &&
                cmd.OutcomeType == ReservationOutcomeType.Confirmed &&
                cmd.EventType == "ticketing.v1.issued"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessMessageAsync_ShouldDispatchCancelledByRefundOutcome_WhenPaymentRefundedMessageReceived()
    {
        // Arrange
        var reservationId = Guid.NewGuid();
        var rawJson = JsonSerializer.Serialize(new
        {
            eventId = Guid.NewGuid().ToString(),
            eventType = "payment.v1.refunded",
            reservationId,
            reason = "SEAT_ALREADY_ALLOCATED"
        });

        var consumeResult = new ConsumeResult<string, string>
        {
            Topic = "payment.v1.refunded",
            Partition = new Partition(0),
            Offset = new Offset(20),
            Message = new Message<string, string>
            {
                Key = reservationId.ToString(),
                Value = rawJson
            }
        };

        _mockRepository
            .Setup(r => r.IsMessageProcessedAsync(It.IsAny<string>(), _options.GroupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockHandler
            .Setup(h => h.HandleAsync(It.IsAny<ReconcileReservationOutcomeCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReconcileReservationOutcomeResult(true, reservationId, "CANCELLED", 2, "AUTO_REFUND_SEAT_COLLISION"));

        var worker = new ReservationCompensationWorker(
            _mockKafkaConsumer.Object,
            _scopeFactory,
            Options.Create(_options),
            NullLogger<ReservationCompensationWorker>.Instance);

        // Act
        await worker.ProcessMessageAsync(consumeResult, CancellationToken.None);

        // Assert
        _mockHandler.Verify(h => h.HandleAsync(
            It.Is<ReconcileReservationOutcomeCommand>(cmd =>
                cmd.ReservationId == reservationId &&
                cmd.OutcomeType == ReservationOutcomeType.CancelledByRefund &&
                cmd.Reason == "AUTO_REFUND_SEAT_COLLISION"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessMessageAsync_ShouldSkipHandler_WhenMessageAlreadyProcessedInInbox()
    {
        // Arrange
        var reservationId = Guid.NewGuid();
        var rawJson = JsonSerializer.Serialize(new
        {
            eventId = "dup-event-123",
            eventType = "payment.v1.declined",
            reservationId
        });

        var consumeResult = new ConsumeResult<string, string>
        {
            Topic = "payment.v1.declined",
            Partition = new Partition(0),
            Offset = new Offset(30),
            Message = new Message<string, string>
            {
                Key = reservationId.ToString(),
                Value = rawJson
            }
        };

        _mockRepository
            .Setup(r => r.IsMessageProcessedAsync("dup-event-123", _options.GroupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var worker = new ReservationCompensationWorker(
            _mockKafkaConsumer.Object,
            _scopeFactory,
            Options.Create(_options),
            NullLogger<ReservationCompensationWorker>.Instance);

        // Act
        await worker.ProcessMessageAsync(consumeResult, CancellationToken.None);

        // Assert
        _mockHandler.Verify(h => h.HandleAsync(It.IsAny<ReconcileReservationOutcomeCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessMessageAsync_ShouldRouteToDlq_WhenJsonIsPoisoned()
    {
        // Arrange
        const string poisonedJson = "corrupted-json{{{";
        var consumeResult = new ConsumeResult<string, string>
        {
            Topic = "payment.v1.refunded",
            Partition = new Partition(0),
            Offset = new Offset(40),
            Message = new Message<string, string>
            {
                Key = "bad-key",
                Value = poisonedJson
            }
        };

        var worker = new ReservationCompensationWorker(
            _mockKafkaConsumer.Object,
            _scopeFactory,
            Options.Create(_options),
            NullLogger<ReservationCompensationWorker>.Instance);

        // Act
        await worker.ProcessMessageAsync(consumeResult, CancellationToken.None);

        // Assert
        _mockKafkaConsumer.Verify(k => k.SendToDlqAsync(
            "bad-key",
            poisonedJson,
            It.Is<string>(reason => reason.Contains("deserialization")),
            It.IsAny<Headers>(),
            It.IsAny<CancellationToken>()),
            Times.Once);

        _mockHandler.Verify(h => h.HandleAsync(It.IsAny<ReconcileReservationOutcomeCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
