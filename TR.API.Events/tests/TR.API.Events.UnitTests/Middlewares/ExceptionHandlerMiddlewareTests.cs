namespace TR.API.Events.UnitTests.Middlewares;

using System.IO;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using TR.API.Events.Domain.Exceptions;
using TR.API.Events.WebApi.Contracts;
using TR.API.Events.WebApi.Middlewares;
using Xunit;

public class ExceptionHandlerMiddlewareTests
{
    private readonly Mock<ILogger<ExceptionHandlerMiddleware>> _loggerMock = new();

    [Fact]
    public async Task InvokeAsync_WhenValidationExceptionThrown_ShouldReturn400JsonResponse()
    {
        // Arrange
        var middleware = new ExceptionHandlerMiddleware(
            _ => throw new ValidationException("Validation failed", new[] { "Slug cannot be empty" }),
            _loggerMock.Object);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        context.Response.ContentType.Should().StartWith("application/json");

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var json = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var response = JsonSerializer.Deserialize<JsonResponse<object, string[]>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        response.Should().NotBeNull();
        response!.StatusCode.Should().Be(400);
        response.Errors.Should().Contain("Slug cannot be empty");
    }

    [Fact]
    public async Task InvokeAsync_WhenNotFoundExceptionThrown_ShouldReturn404JsonResponse()
    {
        // Arrange
        var middleware = new ExceptionHandlerMiddleware(
            _ => throw new NotFoundException("Event not found"),
            _loggerMock.Object);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var json = await new StreamReader(context.Response.Body).ReadToEndAsync();
        var response = JsonSerializer.Deserialize<JsonResponse<object, string[]>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        response.Should().NotBeNull();
        response!.StatusCode.Should().Be(404);
        response.Errors.Should().Contain("Event not found");
    }

    [Fact]
    public async Task InvokeAsync_WhenUnhandledExceptionThrown_ShouldReturn500JsonResponse()
    {
        // Arrange
        var middleware = new ExceptionHandlerMiddleware(
            _ => throw new InvalidOperationException("Unexpected crash"),
            _loggerMock.Object);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
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
