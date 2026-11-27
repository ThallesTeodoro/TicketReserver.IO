namespace TR.HW.Refund.UnitTests.Infrastructure;

using System.Text;
using Confluent.Kafka;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using TR.HW.Refund.Domain.Events;
using TR.HW.Refund.Infrastructure.Configuration;
using TR.HW.Refund.Infrastructure.Messaging;

public sealed class KafkaRefundProducerTests
{
    private readonly Mock<IProducer<string, string>> _mockProducer;
    private readonly KafkaProducerOptions _options;

    public KafkaRefundProducerTests()
    {
        _mockProducer = new Mock<IProducer<string, string>>();
        _options = new KafkaProducerOptions
        {
            BootstrapServers = "localhost:9092",
            PaymentRefundedTopic = "payment.v1.refunded",
            DeadLetterQueueTopic = "ticketing.v1.failed.DLQ"
        };
    }

    [Fact]
    public async Task PublishPaymentRefundedAsync_ShouldProduceMessageWithTraceparentHeader()
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
                Topic = _options.PaymentRefundedTopic,
                Partition = new Partition(0),
                Offset = new Offset(15)
            });

        var producer = new KafkaRefundProducer(
            Options.Create(_options),
            NullLogger<KafkaRefundProducer>.Instance,
            _mockProducer.Object);

        var domainEvent = new PaymentRefundedDomainEvent(
            RefundId: Guid.NewGuid(),
            PaymentId: Guid.NewGuid(),
            ReservationId: Guid.NewGuid(),
            CustomerId: Guid.NewGuid(),
            Amount: 250.00m,
            Currency: "BRL",
            GatewayRefundId: "re_test_999",
            Reason: "SEAT_ALREADY_ALLOCATED",
            OccurredOn: DateTime.UtcNow);

        const string customTraceparent = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";

        // Act
        await producer.PublishPaymentRefundedAsync(domainEvent, customTraceparent);

        // Assert
        capturedTopic.Should().Be(_options.PaymentRefundedTopic);
        capturedMessage.Should().NotBeNull();
        capturedMessage!.Key.Should().Be(domainEvent.ReservationId.ToString());
        capturedMessage.Value.Should().Contain(domainEvent.RefundId.ToString());
        capturedMessage.Value.Should().Contain("re_test_999");
        capturedMessage.Value.Should().Contain("payment.v1.refunded");

        var traceHeader = capturedMessage.Headers.FirstOrDefault(h => h.Key == "traceparent");
        traceHeader.Should().NotBeNull();
        Encoding.UTF8.GetString(traceHeader!.GetValueBytes()).Should().Be(customTraceparent);
    }
}
