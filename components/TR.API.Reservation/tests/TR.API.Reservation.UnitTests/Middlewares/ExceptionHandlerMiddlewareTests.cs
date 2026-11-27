namespace TR.API.Reservation.UnitTests.Middlewares;

using System.IO;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using TR.API.Reservation.Domain.Exceptions;
using TR.API.Reservation.WebApi.Contracts;
using TR.API.Reservation.WebApi.Middlewares;

public class ExceptionHandlerMiddlewareTests
{
    private readonly Mock<ILogger<ExceptionHandlerMiddleware>> _loggerMock = new();

    [Fact]
    public async Task InvokeAsync_WhenValidationExceptionThrown_ShouldReturn400BadRequest()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var middleware = new ExceptionHandlerMiddleware(
            _ => throw new ValidationException("Falha de validação", new[] { "Campo inválido" }),
            _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        context.Response.ContentType.Should().StartWith("application/json");

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var json = await reader.ReadToEndAsync();

        var envelope = JsonSerializer.Deserialize<JsonResponse<object, string[]>>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        envelope.Should().NotBeNull();
        envelope!.StatusCode.Should().Be(400);
        envelope.Message.Should().Be("Falha de validação");
        envelope.Errors.Should().Contain("Campo inválido");
    }

    [Fact]
    public async Task InvokeAsync_WhenConflictExceptionThrown_ShouldReturn409Conflict()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var middleware = new ExceptionHandlerMiddleware(
            _ => throw new ConflictException("Reserva expirada"),
            _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task InvokeAsync_WhenNotFoundExceptionThrown_ShouldReturn404NotFound()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var middleware = new ExceptionHandlerMiddleware(
            _ => throw new NotFoundException("Reserva não encontrada"),
            _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task InvokeAsync_WhenUnhandledExceptionThrown_ShouldReturn500InternalServerError()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var middleware = new ExceptionHandlerMiddleware(
            _ => throw new InvalidOperationException("Erro inesperado no servidor"),
            _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }
}
