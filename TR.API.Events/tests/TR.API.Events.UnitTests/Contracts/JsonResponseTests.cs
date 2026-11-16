namespace TR.API.Events.UnitTests.Contracts;

using FluentAssertions;
using TR.API.Events.WebApi.Contracts;
using Xunit;

public class JsonResponseTests
{
    [Fact]
    public void Success_ShouldCreateValidEnvelope()
    {
        // Arrange
        var payload = new { EventId = Guid.NewGuid(), Name = "Festival Rock" };

        // Act
        var response = JsonResponse<object, object>.Success(payload, 200, "OK");

        // Assert
        response.Should().NotBeNull();
        response.StatusCode.Should().Be(200);
        response.Message.Should().Be("OK");
        response.Data.Should().Be(payload);
        response.Errors.Should().BeNull();
    }

    [Fact]
    public void Failure_ShouldCreateErrorEnvelope()
    {
        // Arrange
        var errors = new[] { "Name is required", "Invalid date window" };

        // Act
        var response = JsonResponse<object, string[]>.Failure(400, errors, "Bad Request");

        // Assert
        response.Should().NotBeNull();
        response.StatusCode.Should().Be(400);
        response.Message.Should().Be("Bad Request");
        response.Data.Should().BeNull();
        response.Errors.Should().BeEquivalentTo(errors);
    }
}
