namespace TR.HW.Payment.Infrastructure.Persistence;

using System.Data.Common;
using Microsoft.Extensions.Options;
using Npgsql;
using TR.HW.Payment.Infrastructure.Configuration;

public interface IDbConnectionFactory
{
    Task<DbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default);
}

public sealed class NpgsqlConnectionFactory(IOptions<PaymentDatabaseOptions> options) : IDbConnectionFactory
{
    private readonly string _connectionString = options.Value.ConnectionString;

    public async Task<DbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
