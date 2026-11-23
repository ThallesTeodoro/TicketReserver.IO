using Carter;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using TR.API.Search.Infrastructure.Health;
using TR.API.Search.WebApi.Contracts;

namespace TR.API.Search.WebApi.Endpoints;

public class HealthModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/search/health", async (
            IElasticsearchHealthChecker healthChecker,
            CancellationToken ct) =>
        {
            var isHealthy = await healthChecker.CheckHealthAsync(ct);
            if (!isHealthy)
            {
                return Results.Json(
                    JsonResponse<object, object>.Failure(
                        503,
                        new[] { "ElasticSearch cluster is unhealthy or unreachable" },
                        "Unhealthy"),
                    statusCode: 503);
            }

            return Results.Ok(
                JsonResponse<object, object>.Success(
                    new
                    {
                        status = "Healthy",
                        elasticsearch = "Connected",
                        timestamp = DateTimeOffset.UtcNow
                    }));
        })
        .WithTags("Health")
        .WithName("SearchHealthCheck");
    }
}
