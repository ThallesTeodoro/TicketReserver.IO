using FluentAssertions;
using TR.HW.Notification.Domain.Entities;
using TR.HW.Notification.Domain.Enums;
using TR.HW.Notification.Domain.Exceptions;

namespace TR.HW.Notification.UnitTests.Domain;

public class CustomerNotificationDomainTests
{
    [Fact]
    public void Create_WithValidParameters_ShouldInstantiatePendingNotification()
    {
        // Arrange
        var id = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var recipient = "customer@ticketreserver.io";
        var title = "Seus Ingressos Estão Prontos!";
        var content = "Parabéns, sua reserva foi confirmada e os ingressos foram emitidos.";

        // Act
        var notification = CustomerNotification.Create(
            id,
            reservationId,
            customerId,
            recipient,
            NotificationType.TicketIssuedConfirmation,
            NotificationChannel.Email,
            title,
            content);

        // Assert
        notification.Should().NotBeNull();
        notification.Id.Should().Be(id);
        notification.ReservationId.Should().Be(reservationId);
        notification.CustomerId.Should().Be(customerId);
        notification.Recipient.Should().Be(recipient);
        notification.Type.Should().Be(NotificationType.TicketIssuedConfirmation);
        notification.Channel.Should().Be(NotificationChannel.Email);
        notification.Status.Should().Be(NotificationStatus.Pending);
        notification.Title.Should().Be(title);
        notification.Content.Should().Be(content);
        notification.SentAt.Should().BeNull();
        notification.FailedAt.Should().BeNull();
        notification.FailureReason.Should().BeNull();
    }

    [Theory]
    [InlineData("", "Title", "Content")]
    [InlineData("   ", "Title", "Content")]
    [InlineData("recipient", "", "Content")]
    [InlineData("recipient", "Title", "")]
    public void Create_WithEmptyStrings_ShouldThrowDomainException(string recipient, string title, string content)
    {
        // Arrange
        var id = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var customerId = Guid.NewGuid();

        // Act
        var act = () => CustomerNotification.Create(
            id,
            reservationId,
            customerId,
            recipient,
            NotificationType.TicketIssuedConfirmation,
            NotificationChannel.Email,
            title,
            content);

        // Assert
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_WithEmptyGuids_ShouldThrowDomainException()
    {
        // Act & Assert
        var actId = () => CustomerNotification.Create(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), "a@b.com", NotificationType.TicketIssuedConfirmation, NotificationChannel.Email, "T", "C");
        var actRes = () => CustomerNotification.Create(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), "a@b.com", NotificationType.TicketIssuedConfirmation, NotificationChannel.Email, "T", "C");
        var actCust = () => CustomerNotification.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, "a@b.com", NotificationType.TicketIssuedConfirmation, NotificationChannel.Email, "T", "C");

        actId.Should().Throw<DomainException>().WithMessage("*identifier cannot be empty*");
        actRes.Should().Throw<DomainException>().WithMessage("*Reservation identifier cannot be empty*");
        actCust.Should().Throw<DomainException>().WithMessage("*Customer identifier cannot be empty*");
    }

    [Fact]
    public void MarkAsSent_ShouldTransitionToSentStatusAndSetProviderReference()
    {
        // Arrange
        var notification = CustomerNotification.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "customer@ticketreserver.io",
            NotificationType.TicketIssuedConfirmation,
            NotificationChannel.Email,
            "Title",
            "Content");
        var sentAt = DateTimeOffset.UtcNow;

        // Act
        notification.MarkAsSent("PROVIDER-REF-123", sentAt);

        // Assert
        notification.Status.Should().Be(NotificationStatus.Sent);
        notification.ExternalProviderReference.Should().Be("PROVIDER-REF-123");
        notification.SentAt.Should().Be(sentAt);
        notification.FailureReason.Should().BeNull();
    }

    [Fact]
    public void MarkAsSent_WhenAlreadySent_ShouldBeIdempotent()
    {
        // Arrange
        var notification = CustomerNotification.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "customer@ticketreserver.io",
            NotificationType.TicketIssuedConfirmation,
            NotificationChannel.Email,
            "Title",
            "Content");
        var sentAt = DateTimeOffset.UtcNow;
        notification.MarkAsSent("PROVIDER-REF-1", sentAt);

        // Act
        notification.MarkAsSent("PROVIDER-REF-2", sentAt.AddMinutes(1));

        // Assert
        notification.Status.Should().Be(NotificationStatus.Sent);
        notification.ExternalProviderReference.Should().Be("PROVIDER-REF-1");
    }

    [Fact]
    public void MarkAsFailed_ShouldTransitionToFailedStatus()
    {
        // Arrange
        var notification = CustomerNotification.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "customer@ticketreserver.io",
            NotificationType.TicketIssuedConfirmation,
            NotificationChannel.Email,
            "Title",
            "Content");
        var failedAt = DateTimeOffset.UtcNow;

        // Act
        notification.MarkAsFailed("SMTP Connection Timeout", failedAt);

        // Assert
        notification.Status.Should().Be(NotificationStatus.Failed);
        notification.FailureReason.Should().Be("SMTP Connection Timeout");
        notification.FailedAt.Should().Be(failedAt);
    }

    [Fact]
    public void MarkAsFailed_WhenAlreadySent_ShouldThrowDomainException()
    {
        // Arrange
        var notification = CustomerNotification.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "customer@ticketreserver.io",
            NotificationType.TicketIssuedConfirmation,
            NotificationChannel.Email,
            "Title",
            "Content");
        notification.MarkAsSent("REF-123", DateTimeOffset.UtcNow);

        // Act
        var act = () => notification.MarkAsFailed("Failure", DateTimeOffset.UtcNow);

        // Assert
        act.Should().Throw<DomainException>().WithMessage("*Cannot mark an already sent notification as failed*");
    }
}
