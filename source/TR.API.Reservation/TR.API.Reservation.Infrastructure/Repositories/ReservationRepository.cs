using System.Data;
using System.Text.Json;
using Dapper;
using Npgsql;
using Polly;
using Polly.Registry;
using TR.API.Reservation.Domain.Entities;
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

                const string outboxSql = @"
                    INSERT INTO OutboxMessages (Id, CorrelationId, EventType, Payload, CreatedAt)
                    VALUES (@Id, @CorrelationId, @EventType, @Payload, @CreatedAt)";

                var payload = JsonSerializer.Serialize(new
                {
                    reservationId = reservation.Id,
                    userId = reservation.UserId,
                    eventId = reservation.EventId,
                    seatNumber = reservation.SeatNumber,
                    ticketPrice = reservation.Price,
                    createdAt = reservation.CreatedAt
                });

                await connection.ExecuteAsync(outboxSql, new
                {
                    Id = Guid.NewGuid(),
                    CorrelationId = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString(),
                    EventType = "reservation.created",
                    Payload = payload,
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
            const string sql = "SELECT * FROM Reservations WHERE Id = @Id";
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
