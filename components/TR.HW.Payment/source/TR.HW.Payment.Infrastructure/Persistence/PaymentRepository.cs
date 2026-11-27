namespace TR.HW.Payment.Infrastructure.Persistence;

using System.Text.Json;
using Dapper;
using TR.HW.Payment.Application.Interfaces;
using TR.HW.Payment.Domain.Entities;
using TR.HW.Payment.Domain.Events;

public sealed class PaymentRepository(IDbConnectionFactory connectionFactory) : IPaymentRepository
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

    public async Task<bool> HasExistingPaymentForReservationAsync(Guid reservationId, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateConnectionAsync(ct);
        const string sql = """
            SELECT 1 FROM payments
            WHERE reservation_id = @ReservationId AND status = 'CAPTURED'::payment_status_enum
            LIMIT 1;
            """;

        var result = await connection.ExecuteScalarAsync<int?>(
            new CommandDefinition(sql, new { ReservationId = reservationId }, cancellationToken: ct));

        return result.HasValue;
    }

    public async Task SavePaymentTransactionallyAsync(Payment payment, string messageId, string consumerGroup, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        try
        {
            const string insertPaymentSql = """
                INSERT INTO payments (
                    id, reservation_id, customer_id, amount, currency, payment_method, status,
                    gateway_provider, gateway_transaction_id, authorization_code, card_last_four,
                    card_brand, failure_reason, version, created_at, updated_at
                ) VALUES (
                    @Id, @ReservationId, @CustomerId, @Amount, @Currency, @PaymentMethod::payment_method_enum,
                    @Status::payment_status_enum, @GatewayProvider, @GatewayTransactionId, @AuthorizationCode,
                    @CardLastFour, @CardBrand, @FailureReason, @Version, @CreatedAt, @UpdatedAt
                );
                """;

            var paymentStatusStr = payment.Status switch
            {
                Domain.Enums.PaymentStatus.Captured => "CAPTURED",
                Domain.Enums.PaymentStatus.Declined => "DECLINED",
                Domain.Enums.PaymentStatus.Authorized => "AUTHORIZED",
                Domain.Enums.PaymentStatus.Failed => "FAILED",
                Domain.Enums.PaymentStatus.Refunded => "REFUNDED",
                Domain.Enums.PaymentStatus.PartiallyRefunded => "PARTIALLY_REFUNDED",
                _ => "PENDING"
            };

            var paymentMethodStr = payment.PaymentMethod switch
            {
                Domain.Enums.PaymentMethod.DebitCard => "DEBIT_CARD",
                Domain.Enums.PaymentMethod.Pix => "PIX",
                _ => "CREDIT_CARD"
            };

            await connection.ExecuteAsync(new CommandDefinition(
                insertPaymentSql,
                new
                {
                    payment.Id,
                    payment.ReservationId,
                    payment.CustomerId,
                    payment.Amount,
                    payment.Currency,
                    PaymentMethod = paymentMethodStr,
                    Status = paymentStatusStr,
                    payment.GatewayProvider,
                    payment.GatewayTransactionId,
                    payment.AuthorizationCode,
                    payment.CardLastFour,
                    payment.CardBrand,
                    payment.FailureReason,
                    payment.Version,
                    payment.CreatedAt,
                    payment.UpdatedAt
                },
                transaction: transaction,
                cancellationToken: ct));

            const string insertTxSql = """
                INSERT INTO payment_transactions (
                    id, payment_id, transaction_type, amount, status, gateway_reference,
                    request_payload, response_payload, error_details, created_at
                ) VALUES (
                    @Id, @PaymentId, @TransactionType::transaction_type_enum, @Amount,
                    @Status::transaction_status_enum, @GatewayReference, @RequestPayload::jsonb,
                    @ResponsePayload::jsonb, @ErrorDetails, @CreatedAt
                );
                """;

            foreach (var tx in payment.Transactions)
            {
                var txTypeStr = tx.TransactionType switch
                {
                    Domain.Enums.TransactionType.Capture => "CAPTURE",
                    Domain.Enums.TransactionType.Void => "VOID",
                    Domain.Enums.TransactionType.Refund => "REFUND",
                    _ => "AUTHORIZE"
                };

                var txStatusStr = tx.Status == Domain.Enums.TransactionStatus.Success ? "SUCCESS" : "FAILED";

                await connection.ExecuteAsync(new CommandDefinition(
                    insertTxSql,
                    new
                    {
                        tx.Id,
                        tx.PaymentId,
                        TransactionType = txTypeStr,
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
                    @Id, @CorrelationId, @EventType, @AggregateType, @AggregateId, @Payload::jsonb,
                    @Status::payment_outbox_status_enum, 0, NOW()
                );
                """;

            foreach (var evt in payment.DomainEvents)
            {
                var eventType = evt switch
                {
                    PaymentApprovedDomainEvent => "payment.approved",
                    PaymentDeclinedDomainEvent => "payment.declined",
                    _ => "payment.event"
                };

                await connection.ExecuteAsync(new CommandDefinition(
                    insertOutboxSql,
                    new
                    {
                        Id = Guid.NewGuid(),
                        CorrelationId = payment.Id,
                        EventType = eventType,
                        AggregateType = "Payment",
                        AggregateId = payment.Id,
                        Payload = JsonSerializer.Serialize((object)evt),
                        Status = "PENDING"
                    },
                    transaction: transaction,
                    cancellationToken: ct));
            }

            const string insertInboxSql = """
                INSERT INTO inbox_messages (message_id, consumer_group, event_type, processed_at)
                VALUES (@MessageId, @ConsumerGroup, 'reservation.v1.checkout_req', NOW())
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
}
