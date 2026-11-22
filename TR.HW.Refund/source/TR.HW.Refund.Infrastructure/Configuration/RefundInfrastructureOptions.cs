namespace TR.HW.Refund.Infrastructure.Configuration;

public sealed class RefundDatabaseOptions
{
    public const string SectionName = "Database";

    public string ConnectionString { get; set; } = string.Empty;
    public int CommandTimeoutSeconds { get; set; } = 30;
}

public sealed class KafkaProducerOptions
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; set; } = string.Empty;
    public string PaymentRefundedTopic { get; set; } = "payment.v1.refunded";
    public string DeadLetterQueueTopic { get; set; } = "ticketing.v1.failed.DLQ";
    public string ClientId { get; set; } = "tr-hw-refund-producer";
    public int MessageTimeoutMs { get; set; } = 10000;
}

public sealed class RefundGatewayOptions
{
    public const string SectionName = "PaymentGateway";

    public string Provider { get; set; } = "STRIPE";
    public string ApiKey { get; set; } = "test_key";
    public string BaseUrl { get; set; } = "https://api.stripe.com";
    public bool SimulateSuccess { get; set; } = true;
    public int MaxRetries { get; set; } = 3;
}
