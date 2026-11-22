namespace TR.HW.Refund.Worker.Configuration;

public sealed class KafkaConsumerOptions
{
    public const string SectionName = "KafkaConsumer";

    public string BootstrapServers { get; set; } = string.Empty;
    public string Topic { get; set; } = "ticketing.v1.failed";
    public string GroupId { get; set; } = "cg-payment-refund";
    public string AutoOffsetReset { get; set; } = "Earliest";
    public bool EnableAutoCommit { get; set; } = false;
    public int SessionTimeoutMs { get; set; } = 45000;
    public int MaxPollIntervalMs { get; set; } = 300000;
    public string DeadLetterQueueTopic { get; set; } = "ticketing.v1.failed.DLQ";
}
