namespace TR.API.Events.Infrastructure.Persistence;

using System.Data;

public interface IDbConnectionFactory
{
    Task<IDbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default);
}
