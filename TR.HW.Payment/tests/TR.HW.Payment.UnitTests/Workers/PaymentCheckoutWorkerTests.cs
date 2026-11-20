namespace TR.HW.Payment.UnitTests.Workers;

using System.Text.Json;
using Confluent.Kafka;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using TR.HW.Payment.Application.Common.Messaging;
using TR.HW.Payment.Application.Features.ProcessPayment;
using TR.HW.Payment.Application.Interfaces;
using TR.HW.Payment.Worker.Configuration;
using TR.HW.Payment.Worker.Messaging;
using TR.HW.Payment.Worker.Workers;

public sealed class PaymentCheckoutWorkerTests
{
    private readonly Mock<IKafkaConsumer> _mockKafkaConsumer;
    private readonly Mock<IPaymentRepository> _mockRepository;
    private readonly Mock<ICommandHandler<ProcessPaymentCommand, ProcessPaymentResult>> _mockHandler;
    private readonly KafkaConsumerOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;

    public PaymentCheckoutWorkerTests()
    {
        _mockKafkaConsumer = new Mock<IKafkaConsumer>();
        _mockRepository = new Mock<IPaymentRepository>();
        _mockHandler = new Mock<ICommandHandler<ProcessPaymentCommand, ProcessPaymentResult>>();

        _options = new KafkaConsumerOptions
        {
            Topic = "reservation.v1.checkout_req",
            GroupId = "cg-payment-processing",
            DeadLetterQueueTopic = "reservation.v1.checkout_req.DLQ"
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
        var customerId = Guid.NewGuid();
        var payloadObj = new
        {
            reservationId,
            customerId,
            totalAmount = 150.00m,
            currency = "BRL",
            paymentMethod = "CREDIT_CARD",
            cardLastFour = "1234",
            cardBrand = "VISA"
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
            .Setup(h => h.HandleAsync(It.IsAny<ProcessPaymentCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessPaymentResult(true, Guid.NewGuid(), "CAPTURED", "ch_123", null));

        var worker = new PaymentCheckoutWorker(
            _mockKafkaConsumer.Object,
            _scopeFactory,
            Options.Create(_options),
            NullLogger<PaymentCheckoutWorker>.Instance);

        // Act
        await worker.ProcessMessageAsync(consumeResult, CancellationToken.None);

        // Assert
        _mockHandler.Verify(h => h.HandleAsync(
            It.Is<ProcessPaymentCommand>(cmd => cmd.ReservationId == reservationId && cmd.TotalAmount == 150.00m),
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
            customerId = Guid.NewGuid(),
            totalAmount = 200.00m,
            currency = "BRL",
            paymentMethod = "CREDIT_CARD"
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

        var worker = new PaymentCheckoutWorker(
            _mockKafkaConsumer.Object,
            _scopeFactory,
            Options.Create(_options),
            NullLogger<PaymentCheckoutWorker>.Instance);

        // Act
        await worker.ProcessMessageAsync(consumeResult, CancellationToken.None);

        // Assert
        _mockHandler.Verify(h => h.HandleAsync(It.IsAny<ProcessPaymentCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessMessageAsync_ShouldRouteToDlq_WhenJsonIsPoisoned()
    {
        // Arrange
        const string poisonedJson = "invalid-json-content{{{";
        var message = new Message<string, string>
        {
            Key = "corrupted-key",
            Value = poisonedJson
        };

        var consumeResult = new ConsumeResult<string, string>
        {
            Message = message,
            Topic = _options.Topic,
            Partition = new Partition(0),
            Offset = new Offset(30)
        };

        var worker = new PaymentCheckoutWorker(
            _mockKafkaConsumer.Object,
            _scopeFactory,
            Options.Create(_options),
            NullLogger<PaymentCheckoutWorker>.Instance);

        // Act
        await worker.ProcessMessageAsync(consumeResult, CancellationToken.None);

        // Assert
        _mockKafkaConsumer.Verify(k => k.SendToDlqAsync(
            "corrupted-key",
            poisonedJson,
            It.Is<string>(reason => reason.Contains("deserialization")),
            It.IsAny<Headers>(),
            It.IsAny<CancellationToken>()),
            Times.Once);

        _mockHandler.Verify(h => h.HandleAsync(It.IsAny<ProcessPaymentCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
