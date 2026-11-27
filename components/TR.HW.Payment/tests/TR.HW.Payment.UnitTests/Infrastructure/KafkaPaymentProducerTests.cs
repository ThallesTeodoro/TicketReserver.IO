namespace TR.HW.Payment.UnitTests.Infrastructure;

using System.Text;
using Confluent.Kafka;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using TR.HW.Payment.Domain.Events;
using TR.HW.Payment.Infrastructure.Configuration;
using TR.HW.Payment.Infrastructure.Messaging;

public sealed class KafkaPaymentProducerTests
{
    private readonly Mock<IProducer<string, string>> _mockProducer;
    private readonly KafkaProducerOptions _options;

    public KafkaPaymentProducerTests()
    {
        _mockProducer = new Mock<IProducer<string, string>>();
        _options = new KafkaProducerOptions
        {
            BootstrapServers = "localhost:9092",
            PaymentApprovedTopic = "payment.v1.approved",
            PaymentDeclinedTopic = "payment.v1.declined",
            DeadLetterQueueTopic = "payment.v1.checkout_req.DLQ"
        };
    }

    [Fact]
    public async Task PublishPaymentApprovedAsync_ShouldProduceMessageWithTraceparentHeader()
    {
        // Arrange
        Message<string, string>? capturedMessage = null;
        string? capturedTopic = null;

        _mockProducer
            .Setup(p => p.ProduceAsync(
                It.IsAny<string>(),
                It.IsAny<Message<string, string>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, Message<string, string>, CancellationToken>((t, m, _) =>
            {
                capturedTopic = t;
                capturedMessage = m;
            })
            .ReturnsAsync(new DeliveryResult<string, string>
            {
                Topic = _options.PaymentApprovedTopic,
                Partition = new Partition(0),
                Offset = new Offset(42)
            });

        var producer = new KafkaPaymentProducer(
            Options.Create(_options),
            NullLogger<KafkaPaymentProducer>.Instance,
            _mockProducer.Object);

        var domainEvent = new PaymentApprovedDomainEvent(
            PaymentId: Guid.NewGuid(),
            ReservationId: Guid.NewGuid(),
            CustomerId: Guid.NewGuid(),
            Amount: 250.00m,
            Currency: "BRL",
            GatewayTransactionId: "ch_test_123",
            AuthorizationCode: "AUTH_XYZ",
            OccurredOn: DateTime.UtcNow);

        var customTraceparent = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";

        // Act
        await producer.PublishPaymentApprovedAsync(domainEvent, customTraceparent);

        // Assert
        capturedTopic.Should().Be(_options.PaymentApprovedTopic);
        capturedMessage.Should().NotBeNull();
        capturedMessage!.Key.Should().Be(domainEvent.ReservationId.ToString());
        capturedMessage.Value.Should().Contain(domainEvent.PaymentId.ToString());
        capturedMessage.Value.Should().Contain("payment.v1.approved");

        var traceHeader = capturedMessage.Headers.FirstOrDefault(h => h.Key == "traceparent");
        traceHeader.Should().NotBeNull();
        Encoding.UTF8.GetString(traceHeader!.GetValueBytes()).Should().Be(customTraceparent);
    }

    [Fact]
    public async Task PublishPaymentDeclinedAsync_ShouldProduceMessageToDeclinedTopic()
    {
        // Arrange
        Message<string, string>? capturedMessage = null;
        string? capturedTopic = null;

        _mockProducer
            .Setup(p => p.ProduceAsync(
                It.IsAny<string>(),
                It.IsAny<Message<string, string>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, Message<string, string>, CancellationToken>((t, m, _) =>
            {
                capturedTopic = t;
                capturedMessage = m;
            })
            .ReturnsAsync(new DeliveryResult<string, string>
            {
                Topic = _options.PaymentDeclinedTopic,
                Partition = new Partition(0),
                Offset = new Offset(99)
            });

        var producer = new KafkaPaymentProducer(
            Options.Create(_options),
            NullLogger<KafkaPaymentProducer>.Instance,
            _mockProducer.Object);

        var domainEvent = new PaymentDeclinedDomainEvent(
            PaymentId: Guid.NewGuid(),
            ReservationId: Guid.NewGuid(),
            CustomerId: Guid.NewGuid(),
            Amount: 250.00m,
            FailureReason: "INSUFFICIENT_FUNDS",
            OccurredOn: DateTime.UtcNow);

        // Act
        await producer.PublishPaymentDeclinedAsync(domainEvent);

        // Assert
        capturedTopic.Should().Be(_options.PaymentDeclinedTopic);
        capturedMessage.Should().NotBeNull();
        capturedMessage!.Key.Should().Be(domainEvent.ReservationId.ToString());
        capturedMessage.Value.Should().Contain(domainEvent.FailureReason);
        capturedMessage.Value.Should().Contain("payment.v1.declined");
    }
}
