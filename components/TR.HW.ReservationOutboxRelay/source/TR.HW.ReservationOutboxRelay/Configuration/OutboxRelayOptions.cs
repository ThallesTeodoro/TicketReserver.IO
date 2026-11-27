namespace TR.HW.ReservationOutboxRelay.Configuration;

public sealed class OutboxRelayOptions
{
    public const string SectionName = "OutboxRelay";

    public string ConnectionString { get; set; } = string.Empty;
    public int BatchSize { get; set; } = 50;
    public int IntervalMilliseconds { get; set; } = 500;
    public int EmptyDelayMilliseconds { get; set; } = 1000;
    public int MaxRetries { get; set; } = 5;
    public int RetryBaseDelaySeconds { get; set; } = 2;
}
