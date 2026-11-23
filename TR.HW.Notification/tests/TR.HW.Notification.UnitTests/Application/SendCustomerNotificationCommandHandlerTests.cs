using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TR.HW.Notification.Application.Features.SendCustomerNotification;
using TR.HW.Notification.Application.Interfaces;
using TR.HW.Notification.Domain.Entities;
using TR.HW.Notification.Domain.Enums;

namespace TR.HW.Notification.UnitTests.Application;

public class SendCustomerNotificationCommandHandlerTests
{
    private readonly Mock<INotificationInboxStore> _inboxStoreMock;
    private readonly Mock<INotificationDispatcher> _dispatcherMock;
    private readonly Mock<ILogger<SendCustomerNotificationCommandHandler>> _loggerMock;
    private readonly SendCustomerNotificationCommandValidator _validator;
    private readonly SendCustomerNotificationCommandHandler _handler;

    public SendCustomerNotificationCommandHandlerTests()
    {
        _inboxStoreMock = new Mock<INotificationInboxStore>();
        _dispatcherMock = new Mock<INotificationDispatcher>();
        _loggerMock = new Mock<ILogger<SendCustomerNotificationCommandHandler>>();
        _validator = new SendCustomerNotificationCommandValidator();

        _handler = new SendCustomerNotificationCommandHandler(
            _inboxStoreMock.Object,
            _dispatcherMock.Object,
            _validator,
            _loggerMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WithValidCommandAndPushToken_ShouldDispatchBothEmailAndPushAndMarkInbox()
    {
        // Arrange
        var command = new SendCustomerNotificationCommand(
            ReservationId: Guid.NewGuid(),
            CustomerId: Guid.NewGuid(),
            CustomerEmail: "alice@example.com",
            CustomerPushToken: "fcm_token_alice_123",
            NotificationType: NotificationType.TicketIssuedConfirmation,
            SourceEventMessageId: "msg-ticket-issued-001");

        _inboxStoreMock
            .Setup(s => s.HasBeenProcessedAsync("cg-notification-worker", command.SourceEventMessageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _dispatcherMock
            .Setup(d => d.DispatchAsync(It.IsAny<CustomerNotification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationDispatchResult(true, "PROVIDER-OK", null));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.IsProcessed.Should().BeTrue();
        result.WasDuplicate.Should().BeFalse();
        result.DispatchedNotificationIds.Should().HaveCount(2);

        _dispatcherMock.Verify(d => d.DispatchAsync(It.IsAny<CustomerNotification>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _inboxStoreMock.Verify(s => s.MarkAsProcessedAsync("cg-notification-worker", command.SourceEventMessageId, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithoutPushToken_ShouldDispatchOnlyEmail()
    {
        // Arrange
        var command = new SendCustomerNotificationCommand(
            ReservationId: Guid.NewGuid(),
            CustomerId: Guid.NewGuid(),
            CustomerEmail: "bob@example.com",
            CustomerPushToken: null,
            NotificationType: NotificationType.PaymentRefundedNotice,
            SourceEventMessageId: "msg-refund-001");

        _inboxStoreMock
            .Setup(s => s.HasBeenProcessedAsync("cg-notification-worker", command.SourceEventMessageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _dispatcherMock
            .Setup(d => d.DispatchAsync(It.IsAny<CustomerNotification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationDispatchResult(true, "REFUND-EMAIL-OK", null));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsProcessed.Should().BeTrue();
        result.WasDuplicate.Should().BeFalse();
        result.DispatchedNotificationIds.Should().HaveCount(1);

        _dispatcherMock.Verify(d => d.DispatchAsync(
            It.Is<CustomerNotification>(n => n.Channel == NotificationChannel.Email && n.Type == NotificationType.PaymentRefundedNotice),
            It.IsAny<CancellationToken>()), Times.Once);

        _inboxStoreMock.Verify(s => s.MarkAsProcessedAsync("cg-notification-worker", command.SourceEventMessageId, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenAlreadyProcessedInInbox_ShouldReturnDuplicateAndSkipDispatch()
    {
        // Arrange
        var command = new SendCustomerNotificationCommand(
            ReservationId: Guid.NewGuid(),
            CustomerId: Guid.NewGuid(),
            CustomerEmail: "charlie@example.com",
            CustomerPushToken: "token_charlie",
            NotificationType: NotificationType.PaymentDeclinedNotice,
            SourceEventMessageId: "msg-duplicate-001");

        _inboxStoreMock
            .Setup(s => s.HasBeenProcessedAsync("cg-notification-worker", command.SourceEventMessageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsProcessed.Should().BeTrue();
        result.WasDuplicate.Should().BeTrue();
        result.DispatchedNotificationIds.Should().BeEmpty();

        _dispatcherMock.Verify(d => d.DispatchAsync(It.IsAny<CustomerNotification>(), It.IsAny<CancellationToken>()), Times.Never);
        _inboxStoreMock.Verify(s => s.MarkAsProcessedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithInvalidCommand_ShouldReturnValidationError()
    {
        // Arrange
        var command = new SendCustomerNotificationCommand(
            ReservationId: Guid.Empty,
            CustomerId: Guid.Empty,
            CustomerEmail: "invalid-email-string",
            CustomerPushToken: null,
            NotificationType: (NotificationType)999,
            SourceEventMessageId: "");

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsProcessed.Should().BeFalse();
        result.WasDuplicate.Should().BeFalse();
        result.ErrorMessage.Should().Contain("ReservationId is required");
        result.ErrorMessage.Should().Contain("CustomerId is required");
        result.ErrorMessage.Should().Contain("Valid email format is required");
        result.ErrorMessage.Should().Contain("SourceEventMessageId is required");

        _inboxStoreMock.Verify(s => s.HasBeenProcessedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _dispatcherMock.Verify(d => d.DispatchAsync(It.IsAny<CustomerNotification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenDispatcherFails_ShouldMarkAsFailedAndStillMarkInboxProcessed()
    {
        // Arrange
        var command = new SendCustomerNotificationCommand(
            ReservationId: Guid.NewGuid(),
            CustomerId: Guid.NewGuid(),
            CustomerEmail: "david@example.com",
            CustomerPushToken: null,
            NotificationType: NotificationType.PaymentDeclinedNotice,
            SourceEventMessageId: "msg-decline-001");

        _inboxStoreMock
            .Setup(s => s.HasBeenProcessedAsync("cg-notification-worker", command.SourceEventMessageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _dispatcherMock
            .Setup(d => d.DispatchAsync(It.IsAny<CustomerNotification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotificationDispatchResult(false, null, "SMTP 550 Mailbox full"));

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.IsProcessed.Should().BeTrue();
        result.WasDuplicate.Should().BeFalse();
        result.DispatchedNotificationIds.Should().BeEmpty(); // Since dispatch failed

        _inboxStoreMock.Verify(s => s.MarkAsProcessedAsync("cg-notification-worker", command.SourceEventMessageId, null, It.IsAny<CancellationToken>()), Times.Once);
    }
}
