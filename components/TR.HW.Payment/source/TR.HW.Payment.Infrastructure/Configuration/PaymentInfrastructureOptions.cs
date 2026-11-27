namespace TR.HW.Payment.Infrastructure.Configuration;

public sealed class PaymentDatabaseOptions
{
    public const string SectionName = "Database";

    public string ConnectionString { get; set; } = string.Empty;
    public int CommandTimeoutSeconds { get; set; } = 30;
}

public sealed class KafkaProducerOptions
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; set; } = string.Empty;
    public string PaymentApprovedTopic { get; set; } = "payment.v1.approved";
    public string PaymentDeclinedTopic { get; set; } = "payment.v1.declined";
    public string DeadLetterQueueTopic { get; set; } = "payment.v1.checkout_req.DLQ";
    public string ClientId { get; set; } = "tr-hw-payment-producer";
    public int MessageTimeoutMs { get; set; } = 10000;
}

public sealed class PaymentGatewayOptions
{
    public const string SectionName = "PaymentGateway";

    public string Provider { get; set; } = "STRIPE";
    public string ApiKey { get; set; } = "test_key";
    public string BaseUrl { get; set; } = "https://api.stripe.com";
    public bool SimulateSuccess { get; set; } = true;
    public int MaxRetries { get; set; } = 3;
}
