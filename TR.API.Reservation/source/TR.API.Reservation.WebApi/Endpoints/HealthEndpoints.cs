namespace TR.API.Reservation.WebApi.Endpoints;

using Carter;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using TR.API.Reservation.WebApi.Contracts;

public class HealthEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/healthz", () =>
        {
            var data = new
            {
                status = "Healthy",
                service = "TR.API.Reservation",
                timestamp = DateTime.UtcNow
            };

            var response = JsonResponse<object, object>.Success(data, StatusCodes.Status200OK, "Service is healthy");
            return Results.Ok(response);
        })
        .WithName("HealthCheck")
        .WithSummary("Verifica a saúde operacional da API de Reserva")
        .WithOpenApi()
        .Produces<JsonResponse<object, object>>(StatusCodes.Status200OK);
    }
}
