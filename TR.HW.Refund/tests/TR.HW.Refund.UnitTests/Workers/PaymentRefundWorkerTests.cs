namespace TR.HW.Refund.UnitTests.Workers;

using System.Text.Json;
using Confluent.Kafka;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using TR.HW.Refund.Application.Common.Messaging;
using TR.HW.Refund.Application.Features.ProcessRefund;
using TR.HW.Refund.Application.Interfaces;
using TR.HW.Refund.Worker.Configuration;
using TR.HW.Refund.Worker.Messaging;
using TR.HW.Refund.Worker.Workers;

public sealed class PaymentRefundWorkerTests
{
    private readonly Mock<IKafkaConsumer> _mockKafkaConsumer;
    private readonly Mock<IRefundRepository> _mockRepository;
    private readonly Mock<ICommandHandler<ProcessRefundCommand, ProcessRefundResult>> _mockHandler;
    private readonly KafkaConsumerOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;

    public PaymentRefundWorkerTests()
    {
        _mockKafkaConsumer = new Mock<IKafkaConsumer>();
        _mockRepository = new Mock<IRefundRepository>();
        _mockHandler = new Mock<ICommandHandler<ProcessRefundCommand, ProcessRefundResult>>();

        _options = new KafkaConsumerOptions
        {
            Topic = "ticketing.v1.failed",
            GroupId = "cg-payment-refund",
            DeadLetterQueueTopic = "ticketing.v1.failed.DLQ"
        };

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddScoped(_ => _mockRepository.Object);
        serviceCollection.AddScoped(_ => _mockHandler.Object);
        var provider = serviceCollection.BuildServiceProvider();
        _scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
    }

    [Fact]
    public async Task ProcessMessageAsync_ShouldInvokeCommandHandler_WhenMessageIsNewAndValid()
    {
        // Arrange
        var reservationId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        var payloadObj = new
        {
            eventId = Guid.NewGuid().ToString(),
            eventType = "ticketing.v1.failed",
            reservationId,
            paymentId,
            customerId,
            failureReason = "SEAT_ALREADY_ALLOCATED"
        };

        var rawJson = JsonSerializer.Serialize(payloadObj);
        var message = new Message<string, string>
        {
            Key = reservationId.ToString(),
            Value = rawJson
        };

        var consumeResult = new ConsumeResult<string, string>
        {
            Message = message,
            Topic = _options.Topic,
            Partition = new Partition(0),
            Offset = new Offset(10)
        };

        _mockRepository
            .Setup(r => r.IsMessageProcessedAsync(It.IsAny<string>(), _options.GroupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockHandler
            .Setup(h => h.HandleAsync(It.IsAny<ProcessRefundCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessRefundResult(true, Guid.NewGuid(), "REFUNDED", "re_123"));

        var worker = new PaymentRefundWorker(
            _mockKafkaConsumer.Object,
            _scopeFactory,
            Options.Create(_options),
            NullLogger<PaymentRefundWorker>.Instance);

        // Act
        await worker.ProcessMessageAsync(consumeResult, CancellationToken.None);

        // Assert
        _mockHandler.Verify(h => h.HandleAsync(
            It.Is<ProcessRefundCommand>(cmd =>
                cmd.ReservationId == reservationId &&
                cmd.PaymentId == paymentId &&
                cmd.Reason == "SEAT_ALREADY_ALLOCATED"),
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
            reservationId,
            paymentId = Guid.NewGuid(),
            failureReason = "SEAT_ALREADY_ALLOCATED"
        });

        var message = new Message<string, string>
        {
            Key = reservationId.ToString(),
            Value = rawJson
        };

        var consumeResult = new ConsumeResult<string, string>
        {
            Message = message,
            Topic = _options.Topic,
            Partition = new Partition(0),
            Offset = new Offset(20)
        };

        _mockRepository
            .Setup(r => r.IsMessageProcessedAsync(It.IsAny<string>(), _options.GroupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var worker = new PaymentRefundWorker(
            _mockKafkaConsumer.Object,
            _scopeFactory,
            Options.Create(_options),
            NullLogger<PaymentRefundWorker>.Instance);

        // Act
        await worker.ProcessMessageAsync(consumeResult, CancellationToken.None);

        // Assert
        _mockHandler.Verify(h => h.HandleAsync(It.IsAny<ProcessRefundCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessMessageAsync_ShouldRouteToDlq_WhenJsonIsPoisoned()
    {
        // Arrange
        const string poisonedJson = "invalid-json{{{";
        var message = new Message<string, string>
        {
            Key = "bad-key",
            Value = poisonedJson
        };

        var consumeResult = new ConsumeResult<string, string>
        {
            Message = message,
            Topic = _options.Topic,
            Partition = new Partition(0),
            Offset = new Offset(30)
        };

        var worker = new PaymentRefundWorker(
            _mockKafkaConsumer.Object,
            _scopeFactory,
            Options.Create(_options),
            NullLogger<PaymentRefundWorker>.Instance);

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

        _mockHandler.Verify(h => h.HandleAsync(It.IsAny<ProcessRefundCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
