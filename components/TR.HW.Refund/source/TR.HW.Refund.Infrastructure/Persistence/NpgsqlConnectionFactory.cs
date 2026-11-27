namespace TR.HW.Refund.Infrastructure.Persistence;

using System.Data.Common;
using Microsoft.Extensions.Options;
using Npgsql;
using TR.HW.Refund.Infrastructure.Configuration;

public interface IDbConnectionFactory
{
    Task<DbConnection> CreateConnectionAsync(CancellationToken ct = default);
}

public sealed class NpgsqlConnectionFactory(IOptions<RefundDatabaseOptions> options) : IDbConnectionFactory
{
    private readonly string _connectionString = options.Value.ConnectionString;

    public async Task<DbConnection> CreateConnectionAsync(CancellationToken ct = default)
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }
}
