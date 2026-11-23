using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using TR.HW.Notification.Application.Interfaces;
using TR.HW.Notification.Domain.Entities;
using TR.HW.Notification.Domain.Enums;
using TR.HW.Notification.Infrastructure.Configuration;
using TR.HW.Notification.Infrastructure.Gateways;

namespace TR.HW.Notification.Infrastructure.Dispatching;

public sealed class ResilientNotificationDispatcher : INotificationDispatcher
{
    private readonly INotificationProviderGateway _gateway;
    private readonly NotificationInfrastructureOptions _options;
    private readonly ILogger<ResilientNotificationDispatcher> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;
    private readonly AsyncCircuitBreakerPolicy _circuitBreakerPolicy;

    public ResilientNotificationDispatcher(
        INotificationProviderGateway gateway,
        IOptions<NotificationInfrastructureOptions> options,
        ILogger<ResilientNotificationDispatcher> logger)
    {
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _retryPolicy = Policy
            .Handle<HttpRequestException>()
            .Or<TimeoutException>()
            .WaitAndRetryAsync(
                _options.RetryCount,
                attempt => TimeSpan.FromMilliseconds(50 * Math.Pow(2, attempt)) + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 50)),
                (outcome, timespan, retryAttempt, context) =>
                {
                    _logger.LogWarning(
                        "Retry {Attempt} for notification {NotificationId} after {Delay}ms due to: {Message}",
                        retryAttempt,
                        context.GetValueOrDefault("NotificationId", "Unknown"),
                        timespan.TotalMilliseconds,
                        outcome.Message);
                });

        _circuitBreakerPolicy = Policy
            .Handle<HttpRequestException>()
            .Or<TimeoutException>()
            .CircuitBreakerAsync(
                _options.CircuitBreakerThreshold,
                TimeSpan.FromSeconds(_options.CircuitBreakerDurationSeconds),
                onBreak: (outcome, timespan) =>
                {
                    _logger.LogError(
                        "Notification dispatcher circuit breaker OPENED for {Duration}s due to: {Message}",
                        timespan.TotalSeconds,
                        outcome.Message);
                },
                onReset: () =>
                {
                    _logger.LogInformation("Notification dispatcher circuit breaker RESET to normal operation.");
                });
    }

    public async Task<NotificationDispatchResult> DispatchAsync(
        CustomerNotification notification,
        CancellationToken cancellationToken = default)
    {
        var context = new Context
        {
            ["NotificationId"] = notification.Id.ToString()
        };

        var policyWrap = Policy.WrapAsync(_retryPolicy, _circuitBreakerPolicy);

        try
        {
            var providerRef = await policyWrap.ExecuteAsync(async ctx =>
            {
                return notification.Channel switch
                {
                    NotificationChannel.Email => await _gateway.SendEmailAsync(
                        notification.Recipient,
                        notification.Title,
                        notification.Content,
                        notification.MetadataJson,
                        cancellationToken),

                    NotificationChannel.Push => await _gateway.SendPushAsync(
                        notification.Recipient,
                        notification.Title,
                        notification.Content,
                        notification.MetadataJson,
                        cancellationToken),

                    _ => throw new NotSupportedException($"Channel '{notification.Channel}' is not supported.")
                };
            }, context);

            return new NotificationDispatchResult(true, providerRef, null);
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogError(ex, "Circuit breaker is open. Notification {NotificationId} rejected.", notification.Id);
            return new NotificationDispatchResult(false, null, $"Circuit breaker is OPEN: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dispatch notification {NotificationId} through provider", notification.Id);
            return new NotificationDispatchResult(false, null, ex.Message);
        }
    }
}
