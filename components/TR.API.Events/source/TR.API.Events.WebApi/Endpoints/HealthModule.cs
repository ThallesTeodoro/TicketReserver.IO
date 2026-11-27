namespace TR.API.Events.WebApi.Endpoints;

using Carter;
using Dapper;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using StackExchange.Redis;
using TR.API.Events.Infrastructure.Persistence;
using TR.API.Events.WebApi.Contracts;

public sealed class HealthModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/health")
            .WithTags("Health");

        group.MapGet("/", () =>
        {
            var data = new
            {
                status = "Healthy",
                service = "TR.API.Events",
                timestamp = DateTimeOffset.UtcNow
            };

            return Results.Ok(JsonResponse<object, object>.Success(data, StatusCodes.Status200OK, "Service is healthy"));
        })
        .WithName("GetHealth")
        .WithSummary("Liveness check")
        .Produces<JsonResponse<object, object>>(StatusCodes.Status200OK)
        .WithOpenApi();

        group.MapGet("/ready", async (
            IDbConnectionFactory connectionFactory,
            IConnectionMultiplexer redisMultiplexer,
            CancellationToken ct) =>
        {
            try
            {
                using var connection = await connectionFactory.CreateConnectionAsync(ct);
                var dbResult = await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT 1;", cancellationToken: ct));

                var redisDb = redisMultiplexer.GetDatabase();
                var redisLatency = await redisDb.PingAsync();

                var data = new
                {
                    status = "Ready",
                    database = dbResult == 1 ? "Connected" : "Unknown",
                    redis = "Connected",
                    redisPingMs = Math.Round(redisLatency.TotalMilliseconds, 2),
                    timestamp = DateTimeOffset.UtcNow
                };

                return Results.Ok(JsonResponse<object, object>.Success(data, StatusCodes.Status200OK, "Dependencies are ready"));
            }
            catch (Exception ex)
            {
                var errors = new[] { $"Readiness probe failed: {ex.Message}" };
                return Results.Json(
                    JsonResponse<object, object>.Failure(StatusCodes.Status503ServiceUnavailable, errors, "Service Unavailable"),
                    statusCode: StatusCodes.Status503ServiceUnavailable
                );
            }
        })
        .WithName("GetReadiness")
        .WithSummary("Readiness probe verifying PostgreSQL and Redis connectivity")
        .Produces<JsonResponse<object, object>>(StatusCodes.Status200OK)
        .Produces<JsonResponse<object, object>>(StatusCodes.Status503ServiceUnavailable)
        .WithOpenApi();
    }
}
