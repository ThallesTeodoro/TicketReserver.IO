using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using TR.HW.Notification.Application.Interfaces;
using TR.HW.Notification.Infrastructure.Configuration;

namespace TR.HW.Notification.Infrastructure.Caching;

public sealed class RedisNotificationInboxStore : INotificationInboxStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly NotificationInfrastructureOptions _options;
    private readonly ILogger<RedisNotificationInboxStore> _logger;

    public RedisNotificationInboxStore(
        IConnectionMultiplexer redis,
        IOptions<NotificationInfrastructureOptions> options,
        ILogger<RedisNotificationInboxStore> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> HasBeenProcessedAsync(
        string consumerGroup,
        string messageId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(consumerGroup))
            throw new ArgumentException("Consumer group cannot be empty.", nameof(consumerGroup));

        if (string.IsNullOrWhiteSpace(messageId))
            throw new ArgumentException("Message identifier cannot be empty.", nameof(messageId));

        var db = _redis.GetDatabase();
        var key = FormatKey(consumerGroup, messageId);

        try
        {
            return await db.KeyExistsAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query Redis inbox existence for key {Key}", key);
            throw;
        }
    }

    public async Task MarkAsProcessedAsync(
        string consumerGroup,
        string messageId,
        TimeSpan? retention = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(consumerGroup))
            throw new ArgumentException("Consumer group cannot be empty.", nameof(consumerGroup));

        if (string.IsNullOrWhiteSpace(messageId))
            throw new ArgumentException("Message identifier cannot be empty.", nameof(messageId));

        var db = _redis.GetDatabase();
        var key = FormatKey(consumerGroup, messageId);
        var ttl = retention ?? TimeSpan.FromDays(_options.InboxRetentionDays);

        try
        {
            await db.StringSetAsync(key, DateTimeOffset.UtcNow.ToString("O"), ttl, When.Always);
            _logger.LogDebug("Marked message {MessageId} as processed in Redis inbox with key {Key} (TTL: {Ttl})", messageId, key, ttl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark message {MessageId} as processed in Redis inbox for key {Key}", messageId, key);
            throw;
        }
    }

    public static string FormatKey(string consumerGroup, string messageId) =>
        $"notif:inbox:{consumerGroup.Trim()}:{messageId.Trim()}";
}
