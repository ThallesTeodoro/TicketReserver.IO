namespace TR.HW.Ticketing.Infrastructure.Persistence;

using System.Text.Json;
using Dapper;
using Npgsql;
using TR.HW.Ticketing.Application.Interfaces;
using TR.HW.Ticketing.Domain.Entities;
using TR.HW.Ticketing.Domain.Enums;
using TR.HW.Ticketing.Domain.Events;
using TR.HW.Ticketing.Domain.Exceptions;

public sealed class TicketingRepository(IDbConnectionFactory connectionFactory) : ITicketingRepository
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

    public async Task<bool> HasExistingTicketsForReservationAsync(Guid reservationId, CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateConnectionAsync(ct);
        const string sql = """
            SELECT 1 FROM tickets
            WHERE reservation_id = @ReservationId AND status = 'ACTIVE'::ticket_status_enum
            LIMIT 1;
            """;

        var result = await connection.ExecuteScalarAsync<int?>(
            new CommandDefinition(sql, new { ReservationId = reservationId }, cancellationToken: ct));

        return result.HasValue;
    }

    public async Task SaveTicketsAndAllocationsTransactionallyAsync(
        IReadOnlyList<Ticket> tickets,
        IReadOnlyList<SeatAllocation> allocations,
        string messageId,
        string consumerGroup,
        CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        Guid currentEventId = Guid.Empty;
        Guid currentSeatId = Guid.Empty;

        try
        {
            const string insertTicketSql = """
                INSERT INTO tickets (
                    id, ticket_code, qr_code_hash, reservation_id, payment_id, customer_id,
                    event_id, ticket_tier_id, seat_id, seat_label, status, price_paid,
                    issued_at, checked_in_at, revoked_at, revocation_reason, version,
                    created_at, updated_at
                ) VALUES (
                    @Id, @TicketCode, @QrCodeHash, @ReservationId, @PaymentId, @CustomerId,
                    @EventId, @TicketTierId, @SeatId, @SeatLabel, @Status::ticket_status_enum,
                    @PricePaid, @IssuedAt, @CheckedInAt, @RevokedAt, @RevocationReason,
                    @Version, @CreatedAt, @UpdatedAt
                );
                """;

            foreach (var ticket in tickets)
            {
                var statusStr = ticket.Status switch
                {
                    TicketStatus.Used => "USED",
                    TicketStatus.Revoked => "REVOKED",
                    TicketStatus.Blocked => "BLOCKED",
                    _ => "ACTIVE"
                };

                await connection.ExecuteAsync(new CommandDefinition(
                    insertTicketSql,
                    new
                    {
                        ticket.Id,
                        ticket.TicketCode,
                        ticket.QrCodeHash,
                        ticket.ReservationId,
                        ticket.PaymentId,
                        ticket.CustomerId,
                        ticket.EventId,
                        ticket.TicketTierId,
                        ticket.SeatId,
                        ticket.SeatLabel,
                        Status = statusStr,
                        ticket.PricePaid,
                        ticket.IssuedAt,
                        ticket.CheckedInAt,
                        ticket.RevokedAt,
                        ticket.RevocationReason,
                        ticket.Version,
                        ticket.CreatedAt,
                        ticket.UpdatedAt
                    },
                    transaction: transaction,
                    cancellationToken: ct));
            }

            const string insertAllocationSql = """
                INSERT INTO seat_allocations (
                    id, event_id, seat_id, reservation_id, ticket_id, allocation_type, allocated_at
                ) VALUES (
                    @Id, @EventId, @SeatId, @ReservationId, @TicketId,
                    @AllocationType::allocation_type_enum, @AllocatedAt
                );
                """;

            foreach (var allocation in allocations)
            {
                currentEventId = allocation.EventId;
                currentSeatId = allocation.SeatId;

                var allocTypeStr = allocation.AllocationType == AllocationType.GeneralAdmission
                    ? "GENERAL_ADMISSION"
                    : "NUMBERED_SEAT";

                await connection.ExecuteAsync(new CommandDefinition(
                    insertAllocationSql,
                    new
                    {
                        allocation.Id,
                        allocation.EventId,
                        allocation.SeatId,
                        allocation.ReservationId,
                        allocation.TicketId,
                        AllocationType = allocTypeStr,
                        allocation.AllocatedAt
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
                    'PENDING'::ticketing_outbox_status_enum, 0, NOW()
                );
                """;

            foreach (var ticket in tickets)
            {
                foreach (var evt in ticket.DomainEvents)
                {
                    await connection.ExecuteAsync(new CommandDefinition(
                        insertOutboxSql,
                        new
                        {
                            Id = Guid.NewGuid(),
                            CorrelationId = ticket.ReservationId,
                            EventType = "ticketing.v1.issued",
                            AggregateType = "Ticket",
                            AggregateId = ticket.Id,
                            Payload = JsonSerializer.Serialize((object)evt)
                        },
                        transaction: transaction,
                        cancellationToken: ct));
                }
            }

            const string insertInboxSql = """
                INSERT INTO inbox_messages (message_id, consumer_group, event_type, processed_at)
                VALUES (@MessageId, @ConsumerGroup, 'payment.v1.approved', NOW())
                ON CONFLICT (message_id, consumer_group) DO NOTHING;
                """;

            await connection.ExecuteAsync(new CommandDefinition(
                insertInboxSql,
                new { MessageId = messageId, ConsumerGroup = consumerGroup },
                transaction: transaction,
                cancellationToken: ct));

            await transaction.CommitAsync(ct);
        }
        catch (PostgresException pgEx) when (pgEx.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await transaction.RollbackAsync(ct);
            throw new SeatAlreadyAllocatedException(currentEventId, currentSeatId);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task RecordTicketingFailedTransactionallyAsync(
        TicketingFailedDomainEvent failedEvent,
        string messageId,
        string consumerGroup,
        CancellationToken ct = default)
    {
        await using var connection = await connectionFactory.CreateConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        try
        {
            const string insertOutboxSql = """
                INSERT INTO outbox_messages (
                    id, correlation_id, event_type, aggregate_type, aggregate_id, payload,
                    status, retry_count, created_at
                ) VALUES (
                    @Id, @CorrelationId, 'ticketing.v1.failed', 'Ticket', @AggregateId, @Payload::jsonb,
                    'PENDING'::ticketing_outbox_status_enum, 0, NOW()
                );
                """;

            await connection.ExecuteAsync(new CommandDefinition(
                insertOutboxSql,
                new
                {
                    Id = Guid.NewGuid(),
                    CorrelationId = failedEvent.ReservationId,
                    AggregateId = failedEvent.ReservationId,
                    Payload = JsonSerializer.Serialize(failedEvent)
                },
                transaction: transaction,
                cancellationToken: ct));

            const string insertInboxSql = """
                INSERT INTO inbox_messages (message_id, consumer_group, event_type, processed_at)
                VALUES (@MessageId, @ConsumerGroup, 'payment.v1.approved', NOW())
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
