using Carter;
using TR.API.Reservation.Application.UseCases.CreateReservation;

namespace TR.API.Reservation.WebApi.Endpoints;

public class ReservationEndpoints : CarterModule
{
    public ReservationEndpoints() : base("/v1/reservations")
    {
    }

    public override void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapPost("/", async (CreateReservationCommand command, CreateReservationUseCase useCase) =>
        {
            var result = await useCase.ExecuteAsync(command);
            return Results.Created($"/v1/reservations/{result.Id}", result);
        });
    }
}
