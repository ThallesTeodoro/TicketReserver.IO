namespace TR.API.Events.Infrastructure.Persistence;

using Dapper;
using Microsoft.Extensions.Logging;

public sealed class DatabaseSeeder(IDbConnectionFactory connectionFactory, ILogger<DatabaseSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO venues (id, name, slug, description, address, city, state, country, postal_code, timezone, total_capacity)
            VALUES
                ('11111111-1111-1111-1111-111111111111', 'Allianz Parque', 'allianz-parque', 'Arena multiuso localizada em Sao Paulo', 'Av. Francisco Matarazzo, 1705', 'Sao Paulo', 'SP', 'BR', '05001-200', 'America/Sao_Paulo', 45000),
                ('22222222-2222-2222-2222-222222222222', 'Autodromo de Interlagos', 'autodromo-interlagos', 'Autodromo Jose Carlos Pace para grandes festivais e corridas', 'Av. Sen. Teotonio Vilela, 261', 'Sao Paulo', 'SP', 'BR', '04801-010', 'America/Sao_Paulo', 80000)
            ON CONFLICT (slug) DO NOTHING;

            INSERT INTO seat_sections (id, venue_id, name, code, is_numbered, capacity, display_order)
            VALUES
                ('11111111-1111-1111-1111-111111111101', '11111111-1111-1111-1111-111111111111', 'Pista Premium', 'PISTA-PREM', FALSE, 15000, 1),
                ('11111111-1111-1111-1111-111111111102', '11111111-1111-1111-1111-111111111111', 'Cadeira Inferior', 'CAD-INF', TRUE, 20000, 2),
                ('22222222-2222-2222-2222-222222222201', '22222222-2222-2222-2222-222222222222', 'Gramado Geral', 'GRAMADO', FALSE, 65000, 1),
                ('22222222-2222-2222-2222-222222222202', '22222222-2222-2222-2222-222222222222', 'Lounge VIP', 'LOUNGE-VIP', FALSE, 15000, 2)
            ON CONFLICT (venue_id, code) DO NOTHING;
            """;

        try
        {
            using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken));
            logger.LogInformation("Default venues and seat sections verified/seeded successfully.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Database seeding skipped or failed on startup: {Message}", ex.Message);
        }
    }
}
