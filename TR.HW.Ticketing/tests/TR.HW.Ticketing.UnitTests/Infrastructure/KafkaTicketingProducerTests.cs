namespace TR.HW.Ticketing.UnitTests.Infrastructure;

using System.Text;
using Confluent.Kafka;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using TR.HW.Ticketing.Domain.Events;
using TR.HW.Ticketing.Infrastructure.Configuration;
using TR.HW.Ticketing.Infrastructure.Messaging;

public sealed class KafkaTicketingProducerTests
{
    private readonly Mock<IProducer<string, string>> _mockProducer;
    private readonly KafkaProducerOptions _options;

    public KafkaTicketingProducerTests()
    {
        _mockProducer = new Mock<IProducer<string, string>>();
        _options = new KafkaProducerOptions
        {
            BootstrapServers = "localhost:9092",
            TicketsIssuedTopic = "ticketing.v1.issued",
            TicketingFailedTopic = "ticketing.v1.failed",
            DeadLetterQueueTopic = "payment.v1.approved.DLQ"
        };
    }

    [Fact]
    public async Task PublishTicketsIssuedAsync_ShouldProduceMessageWithTraceparentHeader()
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
                Topic = _options.TicketsIssuedTopic,
                Partition = new Partition(0),
                Offset = new Offset(10)
            });

        var producer = new KafkaTicketingProducer(
            Options.Create(_options),
            NullLogger<KafkaTicketingProducer>.Instance,
            _mockProducer.Object);

        var reservationId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var ticketEvent = new TicketIssuedDomainEvent(
            TicketId: Guid.NewGuid(),
            TicketCode: "TKT-2026-ABCDEF",
            QrCodeHash: "qr_hash_123",
            ReservationId: reservationId,
            PaymentId: paymentId,
            CustomerId: customerId,
            EventId: eventId,
            TicketTierId: Guid.NewGuid(),
            SeatId: Guid.NewGuid(),
            SeatLabel: "A10",
            PricePaid: 150.00m,
            OccurredOn: DateTime.UtcNow);

        const string customTraceparent = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";

        // Act
        await producer.PublishTicketsIssuedAsync(
            reservationId,
            paymentId,
            customerId,
            eventId,
            new[] { ticketEvent },
            customTraceparent);

        // Assert
        capturedTopic.Should().Be(_options.TicketsIssuedTopic);
        capturedMessage.Should().NotBeNull();
        capturedMessage!.Key.Should().Be(reservationId.ToString());
        capturedMessage.Value.Should().Contain("TKT-2026-ABCDEF");
        capturedMessage.Value.Should().Contain("ticketing.v1.issued");

        var traceHeader = capturedMessage.Headers.FirstOrDefault(h => h.Key == "traceparent");
        traceHeader.Should().NotBeNull();
        Encoding.UTF8.GetString(traceHeader!.GetValueBytes()).Should().Be(customTraceparent);
    }

    [Fact]
    public async Task PublishTicketingFailedAsync_ShouldProduceMessageToFailedTopic()
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
                Topic = _options.TicketingFailedTopic,
                Partition = new Partition(0),
                Offset = new Offset(20)
            });

        var producer = new KafkaTicketingProducer(
            Options.Create(_options),
            NullLogger<KafkaTicketingProducer>.Instance,
            _mockProducer.Object);

        var failedEvent = new TicketingFailedDomainEvent(
            ReservationId: Guid.NewGuid(),
            PaymentId: Guid.NewGuid(),
            CustomerId: Guid.NewGuid(),
            EventId: Guid.NewGuid(),
            FailureReason: "SEAT_ALREADY_ALLOCATED",
            OccurredOn: DateTime.UtcNow);

        // Act
        await producer.PublishTicketingFailedAsync(failedEvent);

        // Assert
        capturedTopic.Should().Be(_options.TicketingFailedTopic);
        capturedMessage.Should().NotBeNull();
        capturedMessage!.Key.Should().Be(failedEvent.ReservationId.ToString());
        capturedMessage.Value.Should().Contain("SEAT_ALREADY_ALLOCATED");
        capturedMessage.Value.Should().Contain("ticketing.v1.failed");
    }
}
