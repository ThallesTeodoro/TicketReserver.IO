namespace TR.API.Search.Application.Common.Messaging;

public interface IQuery<out TResponse>
{
}

public interface IQueryHandler<in TQuery, TResponse> where TQuery : IQuery<TResponse>
{
    Task<TResponse> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}
