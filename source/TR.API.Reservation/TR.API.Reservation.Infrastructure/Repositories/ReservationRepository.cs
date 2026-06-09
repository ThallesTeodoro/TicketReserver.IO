using System.Data;
using System.Text.Json;
using Dapper;
using Npgsql;
using Polly;
using Polly.Registry;
using TR.API.Reservation.Domain.Entities;
using TR.API.Reservation.Domain.Enums;
using TR.API.Reservation.Domain.Events;
using TR.API.Reservation.Domain.Repositories;
using ReservationEntity = TR.API.Reservation.Domain.Entities.Reservation;

namespace TR.API.Reservation.Infrastructure.Repositories;

public class ReservationRepository : IReservationRepository
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ResiliencePipeline _resiliencePipeline;

    public ReservationRepository(NpgsqlDataSource dataSource, ResiliencePipelineProvider<string> pipelineProvider)
    {
        _dataSource = dataSource;
        _resiliencePipeline = pipelineProvider.GetPipeline("default");
    }

    public async Task AddAsync(ReservationEntity reservation)
    {
        await _resiliencePipeline.ExecuteAsync(async ct =>
        {
            await using var connection = await _dataSource.OpenConnectionAsync(ct);
            await using var transaction = await connection.BeginTransactionAsync(ct);

            try
            {
                const string reservationSql = @"
                    INSERT INTO Reservations (Id, UserId, EventId, SeatNumber, Price, Status, CreatedAt)
                    VALUES (@Id, @UserId, @EventId, @SeatNumber, @Price, @Status, @CreatedAt)";

                await connection.ExecuteAsync(reservationSql, new
                {
                    reservation.Id,
                    reservation.UserId,
                    reservation.EventId,
                    reservation.SeatNumber,
                    reservation.Price,
                    Status = reservation.Status.ToString(),
                    reservation.CreatedAt
                }, transaction);

                var @event = new ReservationCreated(
                    reservation.Id,
                    reservation.UserId,
                    reservation.EventId,
                    reservation.SeatNumber,
                    reservation.Price,
                    reservation.CreatedAt);

                const string outboxSql = @"
                    INSERT INTO OutboxMessages (Id, CorrelationId, EventName, EventType, Payload, Status, Attempts, CreatedAt)
                    VALUES (@Id, @CorrelationId, @EventName, @EventType, @Payload, @Status, @Attempts, @CreatedAt)";

                var payload = JsonSerializer.Serialize(@event);

                await connection.ExecuteAsync(outboxSql, new
                {
                    Id = Guid.NewGuid(),
                    CorrelationId = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString(),
                    EventName = nameof(ReservationCreated),
                    EventType = "reservation.created", // Tópico Kafka
                    Payload = payload,
                    Status = (short)OutboxStatus.Pendente,
                    Attempts = 0,
                    CreatedAt = DateTime.UtcNow
                }, transaction);

                await transaction.CommitAsync(ct);
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        });
    }

    public async Task<ReservationEntity?> GetByIdAsync(Guid id)
    {
        return await _resiliencePipeline.ExecuteAsync(async ct =>
        {
            await using var connection = await _dataSource.OpenConnectionAsync(ct);
            const string sql = "SELECT Id, UserId, EventId, SeatNumber, Price, Status, CreatedAt FROM Reservations WHERE Id = @Id";
            return await connection.QueryFirstOrDefaultAsync<ReservationEntity>(sql, new { Id = id });
        });
    }

    public async Task UpdateAsync(ReservationEntity reservation)
    {
        await _resiliencePipeline.ExecuteAsync(async ct =>
        {
            await using var connection = await _dataSource.OpenConnectionAsync(ct);
            const string sql = @"
                UPDATE Reservations 
                SET Status = @Status 
                WHERE Id = @Id";

            await connection.ExecuteAsync(sql, new
            {
                Status = reservation.Status.ToString(),
                reservation.Id
            });
        });
    }
}
