namespace TR.HW.ResTimeout.Persistence;

using System.Data.Common;
using Microsoft.Extensions.Options;
using Npgsql;
using TR.HW.ResTimeout.Configuration;

public sealed class NpgsqlConnectionFactory(IOptions<DatabaseOptions> options) : IDbConnectionFactory
{
    private readonly string _connectionString = options.Value.ConnectionString;

    public async Task<DbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
