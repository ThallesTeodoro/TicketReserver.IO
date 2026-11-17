namespace TR.API.Events.WebApi.Endpoints;

using Carter;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using TR.API.Events.Application.Common.Messaging;
using TR.API.Events.Application.Venues.ListVenues;
using TR.API.Events.Domain.Contracts.Persistence;
using TR.API.Events.WebApi.Contracts;

public sealed class VenuesModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/venues")
            .WithTags("Venues");

        group.MapGet("/", async (
            IQueryHandler<ListVenuesQuery, IReadOnlyCollection<VenueReadModel>> handler,
            CancellationToken ct) =>
        {
            var venues = await handler.HandleAsync(new ListVenuesQuery(), ct);
            var response = JsonResponse<IReadOnlyCollection<VenueReadModel>, object>.Success(
                venues,
                StatusCodes.Status200OK);

            return Results.Ok(response);
        })
        .WithName("ListVenues")
        .WithSummary("List available venues and their seat sections")
        .Produces<JsonResponse<IReadOnlyCollection<VenueReadModel>, object>>(StatusCodes.Status200OK)
        .WithOpenApi();
    }
}
