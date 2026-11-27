using FluentAssertions;
using TR.API.Search.WebApi.Contracts;
using Xunit;

namespace TR.API.Search.UnitTests.Contracts;

public class JsonResponseTests
{
    [Fact]
    public void Success_ShouldSetDefaultsAndData()
    {
        var response = JsonResponse<string, object>.Success("test-data", 200, "OK");

        response.StatusCode.Should().Be(200);
        response.Data.Should().Be("test-data");
        response.Errors.Should().BeNull();
        response.Message.Should().Be("OK");
    }

    [Fact]
    public void Failure_ShouldSetStatusCodeAndErrors()
    {
        var errors = new[] { "error 1", "error 2" };
        var response = JsonResponse<object, string[]>.Failure(400, errors, "Bad Request");

        response.StatusCode.Should().Be(400);
        response.Data.Should().BeNull();
        response.Errors.Should().BeEquivalentTo(errors);
        response.Message.Should().Be("Bad Request");
    }
}
