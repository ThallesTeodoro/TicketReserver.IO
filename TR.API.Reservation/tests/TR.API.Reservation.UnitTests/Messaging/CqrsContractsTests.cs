namespace TR.API.Reservation.UnitTests.Messaging;

using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TR.API.Reservation.Application.Common.Messaging;

public class CqrsContractsTests
{
    public sealed record PingCommand(string Message) : ICommand<string>;

    public sealed class PingCommandHandler : ICommandHandler<PingCommand, string>
    {
        public Task<string> HandleAsync(PingCommand command, CancellationToken cancellationToken = default)
        {
            return Task.FromResult($"Pong: {command.Message}");
        }
    }

    [Fact]
    public async Task DefaultSender_WhenHandlerIsRegistered_ShouldResolveAndExecuteCommand()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddTransient<ICommandHandler<PingCommand, string>, PingCommandHandler>();
        var provider = services.BuildServiceProvider();
        var sender = new DefaultSender(provider);

        // Act
        var result = await sender.SendAsync<PingCommand, string>(new PingCommand("TicketReserver"));

        // Assert
        result.Should().Be("Pong: TicketReserver");
    }

    [Fact]
    public async Task DefaultSender_WhenHandlerIsNotRegistered_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        var sender = new DefaultSender(provider);

        // Act
        var act = async () => await sender.SendAsync<PingCommand, string>(new PingCommand("Test"));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*No handler registered*");
    }
}
