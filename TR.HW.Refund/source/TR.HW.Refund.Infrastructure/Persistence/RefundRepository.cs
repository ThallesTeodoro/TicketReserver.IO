namespace TR.HW.Refund.Infrastructure.Persistence;

using System.Text.Json;
using Dapper;
using TR.HW.Refund.Application.Interfaces;
using TR.HW.Refund.Domain.Entities;
using TR.HW.Refund.Domain.Enums;

public sealed class RefundRepository(IDbConnectionFactory connectionFactory) : IRefundRepository
{
    public async Task<bool> IsMessageProcessedAsync(string messageId, string consumerGroup, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateConnectionAsync(ct);
        const string sql = """
            SELECT 1 FROM inbox_messages
            WHERE message_id = @MessageId AND consumer_group = @ConsumerGroup
            LIMIT 1;
            """;

        var result = await connection.ExecuteScalarAsync<int?>(
            new CommandDefinition(sql, new { MessageId = messageId, ConsumerGroup = consumerGroup }, cancellationToken: ct));

        return result.HasValue;
    }

    public async Task<PaymentSnapshot?> GetPaymentByReservationIdAsync(Guid reservationId, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateConnectionAsync(ct);
        const string sql = """
            SELECT
                id AS Id,
                reservation_id AS ReservationId,
                customer_id AS CustomerId,
                amount AS Amount,
                currency AS Currency,
                status::text AS StatusStr,
                gateway_provider AS GatewayProvider,
                gateway_transaction_id AS GatewayTransactionId,
                version AS Version
            FROM payments
            WHERE reservation_id = @ReservationId
            LIMIT 1;
            """;

        var row = await connection.QueryFirstOrDefaultAsync<PaymentRow>(
            new CommandDefinition(sql, new { ReservationId = reservationId }, cancellationToken: ct));

        if (row is null)
        {
            return null;
        }

        var status = row.StatusStr switch
        {
            "CAPTURED" => PaymentStatus.Captured,
            "REFUNDED" => PaymentStatus.Refunded,
            "PARTIALLY_REFUNDED" => PaymentStatus.PartiallyRefunded,
            "DECLINED" => PaymentStatus.Declined,
            "AUTHORIZED" => PaymentStatus.Authorized,
            "FAILED" => PaymentStatus.Failed,
            _ => PaymentStatus.Pending
        };

        return new PaymentSnapshot(
            Id: row.Id,
            ReservationId: row.ReservationId,
            CustomerId: row.CustomerId,
            Amount: row.Amount,
            Currency: row.Currency,
            Status: status,
            GatewayProvider: row.GatewayProvider,
            GatewayTransactionId: row.GatewayTransactionId,
            Version: row.Version);
    }

    public async Task SaveRefundTransactionallyAsync(
        Refund refund,
        string messageId,
        string consumerGroup,
        CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        try
        {
            if (refund.Status == RefundStatus.Completed)
            {
                const string updatePaymentSql = """
                    UPDATE payments
                    SET status = 'REFUNDED'::payment_status_enum,
                        version = version + 1,
                        updated_at = NOW()
                    WHERE id = @PaymentId AND status = 'CAPTURED'::payment_status_enum;
                    """;

                await connection.ExecuteAsync(new CommandDefinition(
                    updatePaymentSql,
                    new { refund.PaymentId },
                    transaction: transaction,
                    cancellationToken: ct));
            }

            const string insertRefundSql = """
                INSERT INTO refunds (
                    id, payment_id, reservation_id, amount, currency, reason,
                    status, gateway_refund_id, gateway_response, created_at, updated_at
                ) VALUES (
                    @Id, @PaymentId, @ReservationId, @Amount, @Currency, @Reason,
                    @Status::refund_status_enum, @GatewayRefundId, @GatewayResponse::jsonb,
                    @CreatedAt, @UpdatedAt
                );
                """;

            var refundStatusStr = refund.Status switch
            {
                RefundStatus.Completed => "COMPLETED",
                RefundStatus.Failed => "FAILED",
                _ => "PENDING"
            };

            await connection.ExecuteAsync(new CommandDefinition(
                insertRefundSql,
                new
                {
                    refund.Id,
                    refund.PaymentId,
                    refund.ReservationId,
                    refund.Amount,
                    refund.Currency,
                    refund.Reason,
                    Status = refundStatusStr,
                    refund.GatewayRefundId,
                    GatewayResponse = refund.GatewayResponse ?? "{}",
                    refund.CreatedAt,
                    refund.UpdatedAt
                },
                transaction: transaction,
                cancellationToken: ct));

            const string insertTxSql = """
                INSERT INTO payment_transactions (
                    id, payment_id, transaction_type, amount, status, gateway_reference,
                    request_payload, response_payload, error_details, created_at
                ) VALUES (
                    @Id, @PaymentId, 'REFUND'::transaction_type_enum, @Amount,
                    @Status::transaction_status_enum, @GatewayReference, @RequestPayload::jsonb,
                    @ResponsePayload::jsonb, @ErrorDetails, @CreatedAt
                );
                """;

            foreach (var tx in refund.Transactions)
            {
                var txStatusStr = tx.Status == TransactionStatus.Success ? "SUCCESS" : "FAILED";

                await connection.ExecuteAsync(new CommandDefinition(
                    insertTxSql,
                    new
                    {
                        tx.Id,
                        tx.PaymentId,
                        tx.Amount,
                        Status = txStatusStr,
                        tx.GatewayReference,
                        RequestPayload = tx.RequestPayload ?? "{}",
                        ResponsePayload = tx.ResponsePayload ?? "{}",
                        tx.ErrorDetails,
                        tx.CreatedAt
                    },
                    transaction: transaction,
                    cancellationToken: ct));
            }

            const string insertOutboxSql = """
                INSERT INTO outbox_messages (
                    id, correlation_id, event_type, aggregate_type, aggregate_id, payload,
                    status, retry_count, created_at
                ) VALUES (
                    @Id, @CorrelationId, 'payment.refunded', 'Payment', @AggregateId, @Payload::jsonb,
                    'PENDING'::payment_outbox_status_enum, 0, NOW()
                );
                """;

            foreach (var evt in refund.DomainEvents)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    insertOutboxSql,
                    new
                    {
                        Id = Guid.NewGuid(),
                        CorrelationId = refund.ReservationId,
                        AggregateId = refund.PaymentId,
                        Payload = JsonSerializer.Serialize((object)evt)
                    },
                    transaction: transaction,
                    cancellationToken: ct));
            }

            const string insertInboxSql = """
                INSERT INTO inbox_messages (message_id, consumer_group, event_type, processed_at)
                VALUES (@MessageId, @ConsumerGroup, 'ticketing.v1.failed', NOW())
                ON CONFLICT (message_id, consumer_group) DO NOTHING;
                """;

            await connection.ExecuteAsync(new CommandDefinition(
                insertInboxSql,
                new { MessageId = messageId, ConsumerGroup = consumerGroup },
                transaction: transaction,
                cancellationToken: ct));

            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task RecordProcessedMessageAsync(string messageId, string consumerGroup, string eventType, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateConnectionAsync(ct);
        const string sql = """
            INSERT INTO inbox_messages (message_id, consumer_group, event_type, processed_at)
            VALUES (@MessageId, @ConsumerGroup, @EventType, NOW())
            ON CONFLICT (message_id, consumer_group) DO NOTHING;
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { MessageId = messageId, ConsumerGroup = consumerGroup, EventType = eventType },
            cancellationToken: ct));
    }

    private sealed class PaymentRow
    {
        public Guid Id { get; set; }
        public Guid ReservationId { get; set; }
        public Guid CustomerId { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "BRL";
        public string StatusStr { get; set; } = "PENDING";
        public string GatewayProvider { get; set; } = "STRIPE";
        public string? GatewayTransactionId { get; set; }
        public int Version { get; set; }
    }
}
