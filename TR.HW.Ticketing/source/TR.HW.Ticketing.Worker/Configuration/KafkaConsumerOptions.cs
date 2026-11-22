namespace TR.HW.Ticketing.Worker.Configuration;

public sealed class KafkaConsumerOptions
{
    public const string SectionName = "KafkaConsumer";

    public string BootstrapServers { get; set; } = string.Empty;
    public string Topic { get; set; } = "payment.v1.approved";
    public string GroupId { get; set; } = "cg-ticketing-worker";
    public string AutoOffsetReset { get; set; } = "Earliest";
    public bool EnableAutoCommit { get; set; } = false;
    public int SessionTimeoutMs { get; set; } = 45000;
    public int MaxPollIntervalMs { get; set; } = 300000;
    public string DeadLetterQueueTopic { get; set; } = "payment.v1.approved.DLQ";
}
