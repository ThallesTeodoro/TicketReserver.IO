namespace TR.API.Events.UnitTests.Messaging;

using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TR.API.Events.Application;
using TR.API.Events.Application.Common.Messaging;
using Xunit;

public sealed record PingCommand(string Message) : ICommand<string>;

public sealed class PingCommandHandler : ICommandHandler<PingCommand, string>
{
    public Task<string> HandleAsync(PingCommand command, CancellationToken cancellationToken = default)
    {
        return Task.FromResult($"Pong: {command.Message}");
    }
}

public class CqrsDispatcherTests
{
    [Fact]
    public async Task SendAsync_ShouldResolveHandlerAndReturnResult()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddApplicationServices();
        services.AddTransient<ICommandHandler<PingCommand, string>, PingCommandHandler>();

        var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<ISender>();

        // Act
        var result = await sender.SendAsync<PingCommand, string>(new PingCommand("Hello"));

        // Assert
        result.Should().Be("Pong: Hello");
    }
}
