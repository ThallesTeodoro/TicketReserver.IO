namespace TR.HW.ReservationOutboxRelay.Persistence;

using System.Data.Common;
using Microsoft.Extensions.Options;
using Npgsql;
using TR.HW.ReservationOutboxRelay.Configuration;

public sealed class NpgsqlConnectionFactory(IOptions<OutboxRelayOptions> options) : IDbConnectionFactory
{
    private readonly string _connectionString = options.Value.ConnectionString;

    public async Task<DbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
