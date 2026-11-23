namespace TR.HW.ResCompensator.Worker.Configuration;

public sealed class KafkaConsumerOptions
{
    public const string SectionName = "KafkaConsumer";

    public string BootstrapServers { get; set; } = string.Empty;
    public string[] Topics { get; set; } =
    [
        "payment.v1.refunded",
        "payment.v1.declined",
        "ticketing.v1.issued"
    ];
    public string GroupId { get; set; } = "cg-reservation-compensator";
    public string AutoOffsetReset { get; set; } = "Earliest";
    public bool EnableAutoCommit { get; set; } = false;
    public int SessionTimeoutMs { get; set; } = 45000;
    public int MaxPollIntervalMs { get; set; } = 300000;
    public string DeadLetterQueueTopic { get; set; } = "payment.v1.refunded.DLQ";
}
