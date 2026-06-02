using Microsoft.AspNetCore.Http;
using TR.API.Reservation.Application.Ports;

namespace TR.API.Reservation.WebApi.Middlewares;

public class IdempotencyMiddleware
{
    private readonly RequestDelegate _next;
    private const string IdempotencyHeader = "X-Idempotency-Key";

    public IdempotencyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IIdempotencyPort idempotencyPort)
    {
        // Idempotência apenas para POST (Criação de Reserva)
        if (context.Request.Method != HttpMethod.Post.Method)
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(IdempotencyHeader, out var idempotencyKey) || string.IsNullOrWhiteSpace(idempotencyKey))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync($"{IdempotencyHeader} header is missing.");
            return;
        }

        // ADR 02: TTL de 24 horas
        var isIdempotent = await idempotencyPort.IsIdempotentAsync(idempotencyKey!, TimeSpan.FromHours(24));

        if (!isIdempotent)
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            await context.Response.WriteAsync("Duplicate request detected.");
            return;
        }

        await _next(context);
    }
}
