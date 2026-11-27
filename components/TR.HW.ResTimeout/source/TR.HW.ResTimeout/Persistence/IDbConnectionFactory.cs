namespace TR.HW.ResTimeout.Persistence;

using System.Data.Common;

public interface IDbConnectionFactory
{
    Task<DbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default);
}
