using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TR.HW.Notification.Domain.Entities;
using TR.HW.Notification.Domain.Enums;
using TR.HW.Notification.Infrastructure.Configuration;
using TR.HW.Notification.Infrastructure.Dispatching;
using TR.HW.Notification.Infrastructure.Gateways;

namespace TR.HW.Notification.UnitTests.Infrastructure;

public class ResilientNotificationDispatcherTests
{
    private readonly Mock<INotificationProviderGateway> _gatewayMock;
    private readonly Mock<ILogger<ResilientNotificationDispatcher>> _loggerMock;
    private readonly IOptions<NotificationInfrastructureOptions> _options;
    private readonly ResilientNotificationDispatcher _dispatcher;

    public ResilientNotificationDispatcherTests()
    {
        _gatewayMock = new Mock<INotificationProviderGateway>();
        _loggerMock = new Mock<ILogger<ResilientNotificationDispatcher>>();
        _options = Microsoft.Extensions.Options.Options.Create(new NotificationInfrastructureOptions
        {
            RetryCount = 2,
            CircuitBreakerThreshold = 5,
            CircuitBreakerDurationSeconds = 10
        });

        _dispatcher = new ResilientNotificationDispatcher(_gatewayMock.Object, _options, _loggerMock.Object);
    }

    [Fact]
    public async Task DispatchAsync_ForEmail_WhenGatewaySucceeds_ShouldReturnSuccess()
    {
        // Arrange
        var notification = CustomerNotification.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "customer@example.com",
            NotificationType.TicketIssuedConfirmation,
            NotificationChannel.Email,
            "Ingressos Prontos",
            "Conteudo");

        _gatewayMock
            .Setup(g => g.SendEmailAsync("customer@example.com", "Ingressos Prontos", "Conteudo", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync("SENDGRID-REF-100");

        // Act
        var result = await _dispatcher.DispatchAsync(notification);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ProviderReference.Should().Be("SENDGRID-REF-100");
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task DispatchAsync_ForPush_WhenGatewaySucceeds_ShouldReturnSuccess()
    {
        // Arrange
        var notification = CustomerNotification.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "fcm_device_token_abc",
            NotificationType.TicketIssuedConfirmation,
            NotificationChannel.Push,
            "Ingressos Prontos",
            "Conteudo");

        _gatewayMock
            .Setup(g => g.SendPushAsync("fcm_device_token_abc", "Ingressos Prontos", "Conteudo", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync("FCM-REF-200");

        // Act
        var result = await _dispatcher.DispatchAsync(notification);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ProviderReference.Should().Be("FCM-REF-200");
    }

    [Fact]
    public async Task DispatchAsync_WhenTransientHttpFailureOccurs_ShouldRetryAndSucceed()
    {
        // Arrange
        var notification = CustomerNotification.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "customer@example.com",
            NotificationType.TicketIssuedConfirmation,
            NotificationChannel.Email,
            "Title",
            "Content");

        var attempt = 0;
        _gatewayMock
            .Setup(g => g.SendEmailAsync("customer@example.com", "Title", "Content", null, It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                attempt++;
                if (attempt == 1)
                    throw new HttpRequestException("Transient gateway timeout 504");
                return Task.FromResult("RETRY-SUCCESS-REF");
            });

        // Act
        var result = await _dispatcher.DispatchAsync(notification);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ProviderReference.Should().Be("RETRY-SUCCESS-REF");
        attempt.Should().Be(2);
    }

    [Fact]
    public async Task DispatchAsync_WhenAllRetriesFail_ShouldReturnFailureResultWithoutThrowing()
    {
        // Arrange
        var notification = CustomerNotification.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "customer@example.com",
            NotificationType.TicketIssuedConfirmation,
            NotificationChannel.Email,
            "Title",
            "Content");

        _gatewayMock
            .Setup(g => g.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Provider socket timeout"));

        // Act
        var result = await _dispatcher.DispatchAsync(notification);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ProviderReference.Should().BeNull();
        result.ErrorMessage.Should().Contain("Provider socket timeout");
    }
}
