namespace TR.HW.ReservationOutboxRelay.Configuration;

public sealed class KafkaProducerOptions
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; set; } = string.Empty;
    public string CheckoutTopic { get; set; } = "reservation.v1.checkout_req";
    public string HoldCreatedDelayedTopic { get; set; } = "reservation.v1.hold_created.delayed";
    public string DeadLetterQueueTopic { get; set; } = "reservation.v1.checkout_req.DLQ";
    public string ClientId { get; set; } = "tr-hw-reservation-outbox-relay";
    public int MessageTimeoutMs { get; set; } = 10000;
}
