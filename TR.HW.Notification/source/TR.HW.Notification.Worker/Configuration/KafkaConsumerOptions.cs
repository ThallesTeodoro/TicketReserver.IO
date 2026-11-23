namespace TR.HW.Notification.Worker.Configuration;

public sealed class KafkaConsumerOptions
{
    public const string SectionName = "KafkaConsumer";

    public string BootstrapServers { get; set; } = "localhost:9092";
    public string[] Topics { get; set; } =
    [
        "ticketing.v1.issued",
        "payment.v1.refunded",
        "payment.v1.declined"
    ];
    public string GroupId { get; set; } = "cg-notification-worker";
    public string AutoOffsetReset { get; set; } = "Earliest";
    public bool EnableAutoCommit { get; set; } = false;
    public int SessionTimeoutMs { get; set; } = 45000;
    public int MaxPollIntervalMs { get; set; } = 300000;
}
