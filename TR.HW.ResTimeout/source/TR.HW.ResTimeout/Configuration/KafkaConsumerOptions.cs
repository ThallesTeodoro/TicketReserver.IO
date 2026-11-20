namespace TR.HW.ResTimeout.Configuration;

public sealed class KafkaConsumerOptions
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; set; } = string.Empty;
    public string Topic { get; set; } = "reservation.v1.hold_created.delayed";
    public string GroupId { get; set; } = "cg-reservation-timeout";
    public string DeadLetterQueueTopic { get; set; } = "reservation.v1.hold_created.delayed.DLQ";
    public string AutoOffsetReset { get; set; } = "Earliest";
    public bool EnableAutoCommit { get; set; } = false;
    public int MaxPollIntervalMs { get; set; } = 300000;
    public int SessionTimeoutMs { get; set; } = 45000;
}
