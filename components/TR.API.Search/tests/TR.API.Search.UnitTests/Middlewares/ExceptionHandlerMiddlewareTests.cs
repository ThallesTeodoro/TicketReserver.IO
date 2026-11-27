using System.Text.Json;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using TR.API.Search.Domain.Exceptions;
using TR.API.Search.WebApi.Contracts;
using TR.API.Search.WebApi.Middlewares;
using Xunit;

namespace TR.API.Search.UnitTests.Middlewares;

public class ExceptionHandlerMiddlewareTests
{
    private readonly Mock<ILogger<ExceptionHandlerMiddleware>> _loggerMock = new();

    [Fact]
    public async Task InvokeAsync_WhenValidationException_ShouldReturn400()
    {
        var middleware = new ExceptionHandlerMiddleware(
            _ => throw new ValidationException(new[] { new ValidationFailure("Query", "Query cannot be null") }),
            _loggerMock.Object);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        context.Response.ContentType.Should().StartWith("application/json");

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var json = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var response = JsonSerializer.Deserialize<JsonResponse<object, object>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        response.Should().NotBeNull();
        response!.StatusCode.Should().Be(400);
        response.Message.Should().Be("Validation failed.");
    }

    [Fact]
    public async Task InvokeAsync_WhenDomainException_ShouldReturn422()
    {
        var middleware = new ExceptionHandlerMiddleware(
            _ => throw new DomainException("Invalid geo coordinates"),
            _loggerMock.Object);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var json = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var response = JsonSerializer.Deserialize<JsonResponse<object, string[]>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        response.Should().NotBeNull();
        response!.StatusCode.Should().Be(422);
        response.Message.Should().Be("Invalid geo coordinates");
    }

    [Fact]
    public async Task InvokeAsync_WhenUnhandledException_ShouldReturn500()
    {
        var middleware = new ExceptionHandlerMiddleware(
            _ => throw new InvalidOperationException("Fatal error"),
            _loggerMock.Object);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var json = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var response = JsonSerializer.Deserialize<JsonResponse<object, string[]>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        response.Should().NotBeNull();
        response!.StatusCode.Should().Be(500);
    }
}
