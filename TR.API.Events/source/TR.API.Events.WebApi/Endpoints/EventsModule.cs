namespace TR.API.Events.WebApi.Endpoints;

using Carter;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using TR.API.Events.Application.Common.Messaging;
using TR.API.Events.Application.Events.CancelEvent;
using TR.API.Events.Application.Events.ChangeEventStatus;
using TR.API.Events.Application.Events.Common;
using TR.API.Events.Application.Events.CreateEvent;
using TR.API.Events.Application.Events.GetEventById;
using TR.API.Events.Application.Events.ListEvents;
using TR.API.Events.Application.Events.UpdateEvent;
using TR.API.Events.Domain.Contracts.Persistence;
using TR.API.Events.WebApi.Contracts;

public sealed record CreateEventRequest(
    Guid VenueId,
    string Title,
    string Slug,
    string? Description,
    string Category,
    DateTimeOffset SalesStartAt,
    DateTimeOffset SalesEndAt,
    DateTimeOffset EventStartAt,
    DateTimeOffset EventEndAt,
    int AgeRestriction = 0,
    string? BannerUrl = null,
    int MaxTicketsPerUser = 4,
    IReadOnlyCollection<CreateTicketTierItem>? TicketTiers = null);

public sealed record UpdateEventRequest(
    Guid VenueId,
    string Title,
    string Slug,
    string? Description,
    string Category,
    DateTimeOffset SalesStartAt,
    DateTimeOffset SalesEndAt,
    DateTimeOffset EventStartAt,
    DateTimeOffset EventEndAt,
    int AgeRestriction = 0,
    string? BannerUrl = null,
    int MaxTicketsPerUser = 4);

public sealed record ChangeEventStatusRequest(string Status);

public sealed class EventsModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/events")
            .WithTags("Events");

        group.MapPost("/", async (
            [FromBody] CreateEventRequest request,
            ICommandHandler<CreateEventCommand, EventResponse> handler,
            CancellationToken ct) =>
        {
            var command = new CreateEventCommand(
                request.VenueId,
                request.Title,
                request.Slug,
                request.Description,
                request.Category,
                request.SalesStartAt,
                request.SalesEndAt,
                request.EventStartAt,
                request.EventEndAt,
                request.AgeRestriction,
                request.BannerUrl,
                request.MaxTicketsPerUser,
                request.TicketTiers);

            var result = await handler.HandleAsync(command, ct);
            var response = JsonResponse<EventResponse, object>.Success(result, StatusCodes.Status201Created, "Event created");

            return Results.Created($"/events/{result.Id}", response);
        })
        .WithName("CreateEvent")
        .WithSummary("Create a new event in DRAFT status")
        .Produces<JsonResponse<EventResponse, object>>(StatusCodes.Status201Created)
        .Produces<JsonResponse<object, object>>(StatusCodes.Status400BadRequest)
        .Produces<JsonResponse<object, object>>(StatusCodes.Status404NotFound)
        .Produces<JsonResponse<object, object>>(StatusCodes.Status422UnprocessableEntity)
        .WithOpenApi();

        group.MapGet("/", async (
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            [FromQuery] string? category,
            [FromQuery] string? status,
            [FromQuery] string? city,
            IQueryHandler<ListEventsQuery, PaginatedEventsResponse> handler,
            CancellationToken ct) =>
        {
            var query = new ListEventsQuery(
                page ?? 1,
                pageSize ?? 10,
                category,
                status,
                city);

            var result = await handler.HandleAsync(query, ct);
            var response = JsonResponse<PaginatedEventsResponse, object>.Success(result, StatusCodes.Status200OK);

            return Results.Ok(response);
        })
        .WithName("ListEvents")
        .WithSummary("List events with pagination and optional filters")
        .Produces<JsonResponse<PaginatedEventsResponse, object>>(StatusCodes.Status200OK)
        .WithOpenApi();

        group.MapGet("/{id:guid}", async (
            Guid id,
            IQueryHandler<GetEventByIdQuery, EventDetailsReadModel> handler,
            CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(new GetEventByIdQuery(id), ct);
            var response = JsonResponse<EventDetailsReadModel, object>.Success(result, StatusCodes.Status200OK);

            return Results.Ok(response);
        })
        .WithName("GetEventById")
        .WithSummary("Get event details by ID")
        .Produces<JsonResponse<EventDetailsReadModel, object>>(StatusCodes.Status200OK)
        .Produces<JsonResponse<object, object>>(StatusCodes.Status404NotFound)
        .WithOpenApi();

        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateEventRequest request,
            ICommandHandler<UpdateEventCommand, EventResponse> handler,
            CancellationToken ct) =>
        {
            var command = new UpdateEventCommand(
                id,
                request.VenueId,
                request.Title,
                request.Slug,
                request.Description,
                request.Category,
                request.SalesStartAt,
                request.SalesEndAt,
                request.EventStartAt,
                request.EventEndAt,
                request.AgeRestriction,
                request.BannerUrl,
                request.MaxTicketsPerUser);

            var result = await handler.HandleAsync(command, ct);
            var response = JsonResponse<EventResponse, object>.Success(result, StatusCodes.Status200OK, "Event updated");

            return Results.Ok(response);
        })
        .WithName("UpdateEvent")
        .WithSummary("Update event details")
        .Produces<JsonResponse<EventResponse, object>>(StatusCodes.Status200OK)
        .Produces<JsonResponse<object, object>>(StatusCodes.Status400BadRequest)
        .Produces<JsonResponse<object, object>>(StatusCodes.Status404NotFound)
        .Produces<JsonResponse<object, object>>(StatusCodes.Status422UnprocessableEntity)
        .WithOpenApi();

        group.MapPatch("/{id:guid}/status", async (
            Guid id,
            [FromBody] ChangeEventStatusRequest request,
            ICommandHandler<ChangeEventStatusCommand, EventResponse> handler,
            CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(new ChangeEventStatusCommand(id, request.Status), ct);
            var response = JsonResponse<EventResponse, object>.Success(result, StatusCodes.Status200OK, "Event status updated");

            return Results.Ok(response);
        })
        .WithName("ChangeEventStatus")
        .WithSummary("Transition event lifecycle status (PUBLISHED, SALES_OPEN, SALES_PAUSED, SOLD_OUT)")
        .Produces<JsonResponse<EventResponse, object>>(StatusCodes.Status200OK)
        .Produces<JsonResponse<object, object>>(StatusCodes.Status404NotFound)
        .Produces<JsonResponse<object, object>>(StatusCodes.Status422UnprocessableEntity)
        .WithOpenApi();

        group.MapDelete("/{id:guid}", async (
            Guid id,
            ICommandHandler<CancelEventCommand, EventResponse> handler,
            CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(new CancelEventCommand(id), ct);
            var response = JsonResponse<EventResponse, object>.Success(result, StatusCodes.Status200OK, "Event cancelled");

            return Results.Ok(response);
        })
        .WithName("CancelEvent")
        .WithSummary("Logically cancel an event (transitions status to CANCELLED)")
        .Produces<JsonResponse<EventResponse, object>>(StatusCodes.Status200OK)
        .Produces<JsonResponse<object, object>>(StatusCodes.Status404NotFound)
        .Produces<JsonResponse<object, object>>(StatusCodes.Status422UnprocessableEntity)
        .WithOpenApi();
    }
}
