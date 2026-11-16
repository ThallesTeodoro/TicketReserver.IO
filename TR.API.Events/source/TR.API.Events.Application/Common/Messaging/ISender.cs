namespace TR.API.Events.Application.Common.Messaging;

public interface ISender
{
    Task SendAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default) where TCommand : ICommand;
    Task<TResponse> SendAsync<TCommand, TResponse>(TCommand command, CancellationToken cancellationToken = default) where TCommand : ICommand<TResponse>;
    Task<TResponse> QueryAsync<TQuery, TResponse>(TQuery query, CancellationToken cancellationToken = default) where TQuery : IQuery<TResponse>;
}

public sealed class DefaultSender(IServiceProvider serviceProvider) : ISender
{
    public async Task SendAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default) where TCommand : ICommand
    {
        var handler = (ICommandHandler<TCommand>?)serviceProvider.GetService(typeof(ICommandHandler<TCommand>))
            ?? throw new InvalidOperationException($"No handler registered for command {typeof(TCommand).Name}");
        await handler.HandleAsync(command, cancellationToken);
    }

    public async Task<TResponse> SendAsync<TCommand, TResponse>(TCommand command, CancellationToken cancellationToken = default) where TCommand : ICommand<TResponse>
    {
        var handler = (ICommandHandler<TCommand, TResponse>?)serviceProvider.GetService(typeof(ICommandHandler<TCommand, TResponse>))
            ?? throw new InvalidOperationException($"No handler registered for command {typeof(TCommand).Name}");
        return await handler.HandleAsync(command, cancellationToken);
    }

    public async Task<TResponse> QueryAsync<TQuery, TResponse>(TQuery query, CancellationToken cancellationToken = default) where TQuery : IQuery<TResponse>
    {
        var handler = (IQueryHandler<TQuery, TResponse>?)serviceProvider.GetService(typeof(IQueryHandler<TQuery, TResponse>))
            ?? throw new InvalidOperationException($"No handler registered for query {typeof(TQuery).Name}");
        return await handler.HandleAsync(query, cancellationToken);
    }
}
