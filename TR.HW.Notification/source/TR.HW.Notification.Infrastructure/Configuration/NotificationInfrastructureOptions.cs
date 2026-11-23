namespace TR.HW.Notification.Infrastructure.Configuration;

public sealed class NotificationInfrastructureOptions
{
    public const string SectionName = "NotificationInfrastructure";

    public string RedisConnectionString { get; set; } = "localhost:6379,abortConnect=false";
    public int InboxRetentionDays { get; set; } = 7;
    public int RetryCount { get; set; } = 3;
    public int CircuitBreakerThreshold { get; set; } = 5;
    public int CircuitBreakerDurationSeconds { get; set; } = 30;
}
