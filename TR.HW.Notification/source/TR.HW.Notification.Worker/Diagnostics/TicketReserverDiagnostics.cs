namespace TicketReserver.Diagnostics;

using System.Diagnostics;
using System.Diagnostics.Metrics;

public static class TicketReserverDiagnostics
{
    public const string ServiceNamespace = "ticketreserver";
    public const string MeterName = "TicketReserver";
    public const string KafkaActivitySourceName = "TicketReserver.Kafka";
    public const string HttpActivitySourceName = "TicketReserver.Http";

    public static readonly ActivitySource KafkaActivitySource = new(KafkaActivitySourceName, "1.0.0");
    public static readonly ActivitySource HttpActivitySource = new(HttpActivitySourceName, "1.0.0");

    public static readonly Meter Meter = new(MeterName, "1.0.0");

    // 1. ticketreserver_redis_lock_acquire_duration_seconds
    public static readonly Histogram<double> RedisLockAcquireDuration = Meter.CreateHistogram<double>(
        "ticketreserver_redis_lock_acquire_duration_seconds",
        unit: "s",
        description: "Duration of Redis distributed lock acquisition in seconds");

    // 2. ticketreserver_reservations_active_total
    private static int _activeReservations;
    public static readonly ObservableGauge<int> ActiveReservations = Meter.CreateObservableGauge(
        "ticketreserver_reservations_active_total",
        () => new Measurement<int>(_activeReservations, new KeyValuePair<string, object?>("status", "AwaitingPayment")),
        unit: "{reservations}",
        description: "Current number of reservations in AwaitingPayment state");

    // 3. ticketreserver_saga_events_total
    public static readonly Counter<long> SagaEventsTotal = Meter.CreateCounter<long>(
        "ticketreserver_saga_events_total",
        unit: "{events}",
        description: "Total number of saga business events processed by topic");

    // 4. ticketreserver_seat_collision_refunds_total
    public static readonly Counter<long> SeatCollisionRefundsTotal = Meter.CreateCounter<long>(
        "ticketreserver_seat_collision_refunds_total",
        unit: "{refunds}",
        description: "Total number of seat collision auto-refund compensations triggered");

    // 5. ticketreserver_outbox_pending_messages
    private static int _outboxPendingMessages;
    public static readonly ObservableGauge<int> OutboxPendingMessages = Meter.CreateObservableGauge(
        "ticketreserver_outbox_pending_messages",
        () => _outboxPendingMessages,
        unit: "{messages}",
        description: "Number of pending outbox messages in PostgreSQL");

    public static void RecordRedisLockAcquireDuration(double seconds, bool acquired, string operation = "SET NX EX")
    {
        RedisLockAcquireDuration.Record(seconds,
            new KeyValuePair<string, object?>("acquired", acquired),
            new KeyValuePair<string, object?>("operation", operation));
    }

    public static void UpdateActiveReservations(int count)
    {
        Interlocked.Exchange(ref _activeReservations, count);
    }

    public static void RecordSagaEvent(string topic, string status = "success")
    {
        SagaEventsTotal.Add(1,
            new KeyValuePair<string, object?>("topic", topic),
            new KeyValuePair<string, object?>("status", status));
    }

    public static void RecordSeatCollisionRefund()
    {
        SeatCollisionRefundsTotal.Add(1);
    }

    public static void UpdateOutboxPendingMessages(int count)
    {
        Interlocked.Exchange(ref _outboxPendingMessages, count);
    }
}
