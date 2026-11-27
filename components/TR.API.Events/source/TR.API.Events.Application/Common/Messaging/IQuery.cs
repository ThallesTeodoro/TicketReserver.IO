namespace TR.API.Events.Application.Common.Messaging;

public interface IQuery<out TResponse>;

public interface IQueryHandler<in TQuery, TResponse> where TQuery : IQuery<TResponse>
{
    Task<TResponse> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}
