using System.Text.Json;
using Confluent.Kafka;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TR.HW.Notification.Application.Common.Messaging;
using TR.HW.Notification.Application.Features.SendCustomerNotification;
using TR.HW.Notification.Domain.Enums;
using TR.HW.Notification.Worker.Configuration;
using TR.HW.Notification.Worker.Messaging;
using TR.HW.Notification.Worker.Workers;

namespace TR.HW.Notification.UnitTests.Workers;

public class NotificationWorkerTests
{
    private readonly Mock<IKafkaConsumer> _kafkaConsumerMock;
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
    private readonly Mock<IServiceScope> _scopeMock;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<ICommandHandler<SendCustomerNotificationCommand, SendCustomerNotificationResult>> _handlerMock;
    private readonly Mock<ILogger<NotificationWorker>> _loggerMock;
    private readonly IOptions<KafkaConsumerOptions> _options;
    private readonly NotificationWorker _worker;

    public NotificationWorkerTests()
    {
        _kafkaConsumerMock = new Mock<IKafkaConsumer>();
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _scopeMock = new Mock<IServiceScope>();
        _serviceProviderMock = new Mock<IServiceProvider>();
        _handlerMock = new Mock<ICommandHandler<SendCustomerNotificationCommand, SendCustomerNotificationResult>>();
        _loggerMock = new Mock<ILogger<NotificationWorker>>();

        _options = Microsoft.Extensions.Options.Options.Create(new KafkaConsumerOptions
        {
            BootstrapServers = "localhost:9092",
            GroupId = "cg-notification-worker",
            Topics = ["ticketing.v1.issued", "payment.v1.refunded", "payment.v1.declined"]
        });

        _scopeFactoryMock.Setup(s => s.CreateScope()).Returns(_scopeMock.Object);
        _scopeMock.Setup(s => s.ServiceProvider).Returns(_serviceProviderMock.Object);
        _serviceProviderMock
            .Setup(p => p.GetService(typeof(ICommandHandler<SendCustomerNotificationCommand, SendCustomerNotificationResult>)))
            .Returns(_handlerMock.Object);

        _worker = new NotificationWorker(_kafkaConsumerMock.Object, _scopeFactoryMock.Object, _options, _loggerMock.Object);
    }

    [Fact]
    public void ParsePayload_ForTicketingIssued_ShouldExtractExpectedFields()
    {
        // Arrange
        var reservationId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var json = JsonSerializer.Serialize(new
        {
            eventId = "evt-ticket-001",
            eventType = "ticketing.v1.issued",
            payload = new
            {
                reservationId,
                customerId,
                customerEmail = "test@example.com",
                customerPushToken = "push-123"
            }
        });

        // Act
        var command = NotificationWorker.ParsePayload(json, reservationId.ToString(), "ticketing.v1.issued");

        // Assert
        command.ReservationId.Should().Be(reservationId);
        command.CustomerId.Should().Be(customerId);
        command.CustomerEmail.Should().Be("test@example.com");
        command.CustomerPushToken.Should().Be("push-123");
        command.NotificationType.Should().Be(NotificationType.TicketIssuedConfirmation);
        command.SourceEventMessageId.Should().Be("evt-ticket-001");
    }

    [Fact]
    public void ParsePayload_ForPaymentRefunded_ShouldExtractExpectedFields()
    {
        // Arrange
        var reservationId = Guid.NewGuid();
        var json = JsonSerializer.Serialize(new
        {
            id = "evt-refund-001",
            eventType = "payment.v1.refunded",
            payload = new
            {
                reservationId,
                reason = "AUTO_REFUND_SEAT_COLLISION"
            }
        });

        // Act
        var command = NotificationWorker.ParsePayload(json, reservationId.ToString(), "payment.v1.refunded");

        // Assert
        command.ReservationId.Should().Be(reservationId);
        command.NotificationType.Should().Be(NotificationType.PaymentRefundedNotice);
        command.SourceEventMessageId.Should().Be("evt-refund-001");
        command.CustomerEmail.Should().Contain("@ticketreserver.io");
    }

    [Fact]
    public void ParsePayload_ForPaymentDeclined_ShouldExtractExpectedFields()
    {
        // Arrange
        var reservationId = Guid.NewGuid();
        var json = JsonSerializer.Serialize(new
        {
            id = "evt-decline-001",
            eventType = "payment.v1.declined",
            payload = new
            {
                reservationId,
                reason = "INSUFFICIENT_FUNDS"
            }
        });

        // Act
        var command = NotificationWorker.ParsePayload(json, reservationId.ToString(), "payment.v1.declined");

        // Assert
        command.ReservationId.Should().Be(reservationId);
        command.NotificationType.Should().Be(NotificationType.PaymentDeclinedNotice);
    }

    [Fact]
    public async Task ProcessMessageAsync_WhenPayloadIsPoisonPill_ShouldRouteToDlqAndNotInvokeHandler()
    {
        // Arrange
        var poisonMessage = new ConsumeResult<string, string>
        {
            Topic = "ticketing.v1.issued",
            Message = new Message<string, string>
            {
                Key = "key-poison",
                Value = "{ invalid-json-payload !!",
                Headers = new Headers()
            }
        };

        // Act
        await _worker.ProcessMessageAsync(poisonMessage, CancellationToken.None);

        // Assert
        _kafkaConsumerMock.Verify(k => k.SendToDlqAsync(
            "ticketing.v1.issued",
            "key-poison",
            "{ invalid-json-payload !!",
            It.Is<string>(r => r.Contains("deserialization error")),
            It.IsAny<Headers>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _handlerMock.Verify(h => h.HandleAsync(It.IsAny<SendCustomerNotificationCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessMessageAsync_WithValidPayload_ShouldInvokeHandlerSuccessfully()
    {
        // Arrange
        var reservationId = Guid.NewGuid();
        var validJson = JsonSerializer.Serialize(new
        {
            eventId = "evt-valid-123",
            eventType = "ticketing.v1.issued",
            payload = new
            {
                reservationId,
                customerEmail = "buyer@example.com"
            }
        });

        var message = new ConsumeResult<string, string>
        {
            Topic = "ticketing.v1.issued",
            Message = new Message<string, string>
            {
                Key = reservationId.ToString(),
                Value = validJson,
                Headers = new Headers()
            }
        };

        _handlerMock
            .Setup(h => h.HandleAsync(It.IsAny<SendCustomerNotificationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SendCustomerNotificationResult(true, false, [Guid.NewGuid()]));

        // Act
        await _worker.ProcessMessageAsync(message, CancellationToken.None);

        // Assert
        _handlerMock.Verify(h => h.HandleAsync(
            It.Is<SendCustomerNotificationCommand>(c => c.ReservationId == reservationId && c.NotificationType == NotificationType.TicketIssuedConfirmation),
            It.IsAny<CancellationToken>()), Times.Once);

        _kafkaConsumerMock.Verify(k => k.SendToDlqAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Headers>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
