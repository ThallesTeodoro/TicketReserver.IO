namespace TR.API.Reservation.WebApi.Endpoints;

using Carter;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using TR.API.Reservation.Application.Common.Messaging;
using TR.API.Reservation.Application.Reservations.CancelReservation;
using TR.API.Reservation.Application.Reservations.CheckoutReservation;
using TR.API.Reservation.Application.Reservations.GetReservationById;
using TR.API.Reservation.Application.Reservations.HoldReservation;
using TR.API.Reservation.WebApi.Contracts;
using DomainValidationException = TR.API.Reservation.Domain.Exceptions.ValidationException;

public sealed record HoldItemRequest(
    Guid TicketTierId,
    Guid? SeatId,
    string? SeatLabel,
    int Quantity,
    decimal UnitPrice);

public sealed record HoldReservationRequest(
    Guid CustomerId,
    Guid EventId,
    IReadOnlyCollection<HoldItemRequest> Items,
    string Currency = "BRL");

public sealed record CheckoutPaymentDetailsRequest(
    string PaymentMethod,
    string? CardToken,
    int? Installments,
    string? CustomerDocument);

public sealed record CheckoutReservationRequest(
    Guid ReservationId,
    Guid CustomerId,
    CheckoutPaymentDetailsRequest PaymentDetails);

public sealed record CancelReservationRequest(
    Guid? CustomerId = null,
    string? Reason = null);

public sealed class ReservationEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/reserva")
            .WithTags("Reservas");

        // 1. POST /reserva/hold
        group.MapPost("/hold", async (
            [FromBody] HoldReservationRequest request,
            IValidator<HoldReservationCommand> validator,
            ICommandHandler<HoldReservationCommand, HoldReservationResponse> handler,
            CancellationToken ct) =>
        {
            var items = request.Items?.Select(i => new HoldReservationItemDto(
                i.TicketTierId,
                i.SeatId,
                i.SeatLabel,
                i.Quantity,
                i.UnitPrice)).ToList() ?? new List<HoldReservationItemDto>();

            var command = new HoldReservationCommand(
                request.CustomerId,
                request.EventId,
                items,
                request.Currency);

            var validationResult = await validator.ValidateAsync(command, ct);
            if (!validationResult.IsValid)
            {
                throw new DomainValidationException("Dados de retenção de reserva inválidos.", validationResult.Errors.Select(e => e.ErrorMessage));
            }

            var result = await handler.HandleAsync(command, ct);
            var response = JsonResponse<HoldReservationResponse, object>.Success(
                result,
                StatusCodes.Status201Created,
                "Hold de reserva criado com sucesso.");

            return Results.Created($"/reserva/{result.Id}", response);
        })
        .WithName("HoldReservation")
        .WithSummary("Cria retenção temporária de assentos e cotas de ingressos (10 minutos)")
        .WithDescription("Adquire locks atômicos no Redis, persiste a reserva pendente no PostgreSQL e agenda evento de outbox.")
        .Produces<JsonResponse<HoldReservationResponse, object>>(StatusCodes.Status201Created)
        .Produces<JsonResponse<object, object>>(StatusCodes.Status400BadRequest)
        .Produces<JsonResponse<object, object>>(StatusCodes.Status409Conflict)
        .Produces<JsonResponse<object, object>>(StatusCodes.Status422UnprocessableEntity);

        // 2. POST /reserva/checkout
        group.MapPost("/checkout", async (
            [FromBody] CheckoutReservationRequest request,
            IValidator<CheckoutReservationCommand> validator,
            ICommandHandler<CheckoutReservationCommand, CheckoutReservationResponse> handler,
            CancellationToken ct) =>
        {
            var paymentDto = new CheckoutPaymentDetailsDto(
                request.PaymentDetails?.PaymentMethod ?? string.Empty,
                request.PaymentDetails?.CardToken,
                request.PaymentDetails?.Installments ?? 1,
                request.PaymentDetails?.CustomerDocument);

            var command = new CheckoutReservationCommand(
                request.ReservationId,
                request.CustomerId,
                paymentDto);

            var validationResult = await validator.ValidateAsync(command, ct);
            if (!validationResult.IsValid)
            {
                throw new DomainValidationException("Dados de checkout inválidos.", validationResult.Errors.Select(e => e.ErrorMessage));
            }

            var result = await handler.HandleAsync(command, ct);
            var response = JsonResponse<CheckoutReservationResponse, object>.Success(
                result,
                StatusCodes.Status202Accepted,
                "Checkout iniciado com sucesso.");

            return Results.Accepted($"/reserva/{result.ReservationId}", response);
        })
        .WithName("CheckoutReservation")
        .WithSummary("Inicia o processo de checkout e transiciona a reserva para PROCESSING_PAYMENT")
        .WithDescription("Valida o prazo de hold, atualiza status com controle de concorrência otimista e gera outbox para a saga de pagamento.")
        .Produces<JsonResponse<CheckoutReservationResponse, object>>(StatusCodes.Status202Accepted)
        .Produces<JsonResponse<object, object>>(StatusCodes.Status400BadRequest)
        .Produces<JsonResponse<object, object>>(StatusCodes.Status404NotFound)
        .Produces<JsonResponse<object, object>>(StatusCodes.Status409Conflict);

        // 3. GET /reserva/{id:guid}
        group.MapGet("/{id:guid}", async (
            Guid id,
            IQueryHandler<GetReservationByIdQuery, ReservationDetailsResponse> handler,
            CancellationToken ct) =>
        {
            var query = new GetReservationByIdQuery(id);
            var result = await handler.HandleAsync(query, ct);
            var response = JsonResponse<ReservationDetailsResponse, object>.Success(
                result,
                StatusCodes.Status200OK,
                "Reserva consultada com sucesso.");

            return Results.Ok(response);
        })
        .WithName("GetReservationById")
        .WithSummary("Consulta os detalhes da reserva pelo identificador")
        .WithDescription("Retorna dados completos da reserva, itens, tempo restante de hold em segundos e indicador de expiração.")
        .Produces<JsonResponse<ReservationDetailsResponse, object>>(StatusCodes.Status200OK)
        .Produces<JsonResponse<object, object>>(StatusCodes.Status404NotFound);

        // 4. DELETE /reserva/{id:guid}
        group.MapDelete("/{id:guid}", async (
            Guid id,
            [FromHeader(Name = "X-Customer-Id")] Guid? headerCustomerId,
            [FromQuery] Guid? customerId,
            [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] CancelReservationRequest? request,
            IValidator<CancelReservationCommand> validator,
            ICommandHandler<CancelReservationCommand, CancelReservationResponse> handler,
            CancellationToken ct) =>
        {
            var effectiveCustomerId = request?.CustomerId ?? customerId ?? headerCustomerId ?? Guid.Empty;
            var reason = request?.Reason ?? "USER_CANCELLED";

            var command = new CancelReservationCommand(id, effectiveCustomerId, reason);
            var validationResult = await validator.ValidateAsync(command, ct);
            if (!validationResult.IsValid)
            {
                throw new DomainValidationException("Identificador do cliente é obrigatório para cancelamento.", validationResult.Errors.Select(e => e.ErrorMessage));
            }

            var result = await handler.HandleAsync(command, ct);
            var response = JsonResponse<CancelReservationResponse, object>.Success(
                result,
                StatusCodes.Status200OK,
                "Reserva cancelada com sucesso.");

            return Results.Ok(response);
        })
        .WithName("CancelReservation")
        .WithSummary("Cancela voluntariamente uma reserva que ainda esteja com status AWAITING_PAYMENT")
        .WithDescription("Transiciona a reserva para CANCELLED e libera imediatamente assentos e cotas retidos no Redis.")
        .Produces<JsonResponse<CancelReservationResponse, object>>(StatusCodes.Status200OK)
        .Produces<JsonResponse<object, object>>(StatusCodes.Status400BadRequest)
        .Produces<JsonResponse<object, object>>(StatusCodes.Status404NotFound)
        .Produces<JsonResponse<object, object>>(StatusCodes.Status409Conflict);
    }
}
