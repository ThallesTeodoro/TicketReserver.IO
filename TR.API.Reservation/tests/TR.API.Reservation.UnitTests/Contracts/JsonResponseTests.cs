namespace TR.API.Reservation.UnitTests.Contracts;

using FluentAssertions;
using TR.API.Reservation.WebApi.Contracts;

public class JsonResponseTests
{
    [Fact]
    public void Success_ShouldCreateValidEnvelopeWithStatusCode200()
    {
        // Act
        var response = JsonResponse<string, object>.Success("Payload Data", 200, "Operação realizada");

        // Assert
        response.StatusCode.Should().Be(200);
        response.Message.Should().Be("Operação realizada");
        response.Data.Should().Be("Payload Data");
        response.Errors.Should().BeNull();
    }

    [Fact]
    public void Failure_ShouldCreateValidEnvelopeWithErrorsAndStatusCode400()
    {
        // Act
        var errors = new[] { "Campo inválido", "Valor obrigatório" };
        var response = JsonResponse<object, string[]>.Failure(400, errors, "Erro de validação");

        // Assert
        response.StatusCode.Should().Be(400);
        response.Message.Should().Be("Erro de validação");
        response.Data.Should().BeNull();
        response.Errors.Should().BeEquivalentTo(errors);
    }
}
