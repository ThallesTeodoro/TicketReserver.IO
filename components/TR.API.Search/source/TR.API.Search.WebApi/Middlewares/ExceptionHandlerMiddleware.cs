using System.Net;
using System.Text.Json;
using FluentValidation;
using TR.API.Search.Domain.Exceptions;
using TR.API.Search.WebApi.Contracts;

namespace TR.API.Search.WebApi.Middlewares;

public sealed class ExceptionHandlerMiddleware(RequestDelegate next, ILogger<ExceptionHandlerMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, message, errors) = exception switch
        {
            ValidationException validationEx => (
                HttpStatusCode.BadRequest,
                "Validation failed.",
                (object)validationEx.Errors.Select(e => new { e.PropertyName, e.ErrorMessage })
            ),
            DomainException domainEx => (
                HttpStatusCode.UnprocessableEntity,
                domainEx.Message,
                (object)new[] { domainEx.Message }
            ),
            _ => (
                HttpStatusCode.InternalServerError,
                "An unexpected internal server error occurred.",
                (object)new[] { exception.Message }
            )
        };

        if (statusCode == HttpStatusCode.InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception occurred: {Message}", exception.Message);
        }
        else
        {
            logger.LogWarning("Handled domain/validation exception: {Message}", message);
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var response = JsonResponse<object, object>.Failure((int)statusCode, errors, message);
        var json = JsonSerializer.Serialize(response, JsonOptions);

        await context.Response.WriteAsync(json);
    }
}
