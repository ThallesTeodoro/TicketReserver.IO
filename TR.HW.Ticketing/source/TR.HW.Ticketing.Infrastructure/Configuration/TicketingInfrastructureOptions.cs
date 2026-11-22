namespace TR.HW.Ticketing.Infrastructure.Configuration;

public sealed class TicketingDatabaseOptions
{
    public const string SectionName = "Database";

    public string ConnectionString { get; set; } = string.Empty;
    public int CommandTimeoutSeconds { get; set; } = 30;
}

public sealed class KafkaProducerOptions
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; set; } = string.Empty;
    public string TicketsIssuedTopic { get; set; } = "ticketing.v1.issued";
    public string TicketingFailedTopic { get; set; } = "ticketing.v1.failed";
    public string DeadLetterQueueTopic { get; set; } = "payment.v1.approved.DLQ";
    public string ClientId { get; set; } = "tr-hw-ticketing-producer";
    public int MessageTimeoutMs { get; set; } = 10000;
}

public sealed class TicketingSecurityOptions
{
    public const string SectionName = "TicketingSecurity";

    public string QrCodeSecretKey { get; set; } = "ticketreserver-default-hmac-secret-key-2026-anti-fraud";
}
