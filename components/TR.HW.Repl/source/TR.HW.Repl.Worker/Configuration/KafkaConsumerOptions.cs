namespace TR.HW.Repl.Worker.Configuration;

public sealed class KafkaConsumerOptions
{
    public const string SectionName = "KafkaConsumer";

    public string BootstrapServers { get; set; } = "localhost:9092";
    public string[] Topics { get; set; } =
    [
        "catalog.events.events",
        "catalog.events.venues",
        "catalog.events.ticket_tiers"
    ];
    public string GroupId { get; set; } = "cg-search-replication-worker";
    public string AutoOffsetReset { get; set; } = "Earliest";
    public bool EnableAutoCommit { get; set; } = false;
    public int SessionTimeoutMs { get; set; } = 45000;
    public int MaxPollIntervalMs { get; set; } = 300000;
}
